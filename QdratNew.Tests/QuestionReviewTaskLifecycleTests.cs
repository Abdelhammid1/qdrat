using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.Services.Interfaces;
using QdratNew.Services.QuestionReviewTasks;
using QdratNew.ViewModels.QuestionReviewTasks;
using Xunit;

namespace QdratNew.Tests
{
    // QRT-S6 — دورة الحياة: معالجة المرتجعات، إزالة عناصر، إعادة إسناد، إلغاء، إغلاق، تمديد، وقواعد الحالة
    public class QuestionReviewTaskLifecycleTests
    {
        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;
            public FixedTimeProvider(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static readonly DateTime Now = new(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc);
        private static readonly ReviewActor Admin = new("admin-1", "مدير النظام", UserRoleType.Admin);
        private static readonly ReviewActor Teacher = new("u-1", "أ. محمد", UserRoleType.Instructor);

        private sealed class Harness
        {
            public required QuestionReviewTaskService Service { get; init; }
            public required TestDbContextFactory Factory { get; init; }
            public required Mock<IAdvancedNotificationService> Notifications { get; init; }
        }

        // allowedCurricula: المناهج المسموحة لكل مدرب (الافتراضي: المنهج 1 للجميع)
        private static Harness Create(Func<int, List<int>>? allowedCurricula = null)
        {
            var factory = new TestDbContextFactory(Guid.NewGuid().ToString());
            var scope = new Mock<IInstructorScopeService>();
            scope.Setup(s => s.GetDirectCurriculumIdsAsync(It.IsAny<int>()))
                 .ReturnsAsync((int id) => allowedCurricula?.Invoke(id) ?? new List<int> { 1 });
            var notifications = new Mock<IAdvancedNotificationService>();

            return new Harness
            {
                Service = new QuestionReviewTaskService(factory, new FixedTimeProvider(new DateTimeOffset(Now)),
                    scope.Object, notifications.Object, NullLogger<QuestionReviewTaskService>.Instance),
                Factory = factory,
                Notifications = notifications
            };
        }

        private static async Task<Instructor> SeedInstructorAsync(TestDbContextFactory factory, string userId, string name, int? partnerId = null)
        {
            using var db = factory.CreateDbContext();
            var i = new Instructor
            {
                FullName = name,
                NationalID = Guid.NewGuid().ToString("N")[..10],
                Email = $"{Guid.NewGuid():N}@test.local",
                Specialization = "عام",
                IsActive = true,
                UserId = userId,
                PartnerId = partnerId
            };
            db.Instructors.Add(i);
            await db.SaveChangesAsync();
            return i;
        }

        private static async Task<List<Question>> SeedQuestionsAsync(TestDbContextFactory factory, int count, Action<Question, int>? customize = null)
        {
            using var db = factory.CreateDbContext();
            if (!await db.Curriculums.AnyAsync(c => c.Id == 1))
            {
                db.Curriculums.Add(new Curriculum { Id = 1, Title = "كمي", Description = "وصف", CurriculumTypeName = "قدرات" });
                db.Sections.Add(new Section { Id = 1, Title = "محور الجبر", CurriculumId = 1 });
                db.Units.Add(new Unit { Id = 1, Title = "وحدة" });
                db.Lessons.Add(new Lesson { Id = 1, Title = "مؤشر المعادلات", Content = "-", UnitId = 1, SectionId = 1 });
                await db.SaveChangesAsync();
            }

            var list = new List<Question>();
            for (var i = 0; i < count; i++)
            {
                var q = new Question
                {
                    Title = $"سؤال {i}",
                    ReferenceNumber = $"Q-{Guid.NewGuid():N}"[..12],
                    CurriculumId = 1,
                    LessonId = 1,
                    SectionId = 1,
                    CorrectAnswer = "أ",
                    IsComplete = true,
                    CreatedAt = new DateTime(2026, 1, 1).AddDays(i),
                    Options = new List<QuestionOption> { new() { Text = "أ" }, new() { Text = "ب" } }
                };
                customize?.Invoke(q, i);
                list.Add(q);
            }
            db.Questions.AddRange(list);
            await db.SaveChangesAsync();
            return list;
        }

        private sealed record Seeded(int TaskId, List<long> ItemIds, List<Guid> QuestionIds, Instructor Instructor);

        private static async Task<Seeded> SeedTaskAsync(Harness h, int count, Action<Question, int>? customize = null, DateTime? dueLocal = null)
        {
            var instructor = await SeedInstructorAsync(h.Factory, "u-1", "أ. محمد");
            var questions = await SeedQuestionsAsync(h.Factory, count, customize);
            var created = await h.Service.CreateTaskAsync(new CreateQuestionReviewTaskInput
            {
                Title = "مراجعة الجبر",
                InstructorId = instructor.Id,
                SelectedQuestionIds = questions.Select(q => q.Id).ToList(),
                DueAtLocal = dueLocal
            }, Admin);
            Assert.True(created.Success, created.Message);

            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            // QuestionIds بنفس ترتيب ItemIds (ترتيب الإدراج من مزوّد InMemory ليس ترتيب البذر)
            var items = await db.QuestionReviewTaskItems.OrderBy(i => i.SortOrder).Select(i => new { i.Id, i.QuestionId }).ToListAsync();
            return new Seeded(task.Id, items.Select(i => i.Id).ToList(), items.Select(i => i.QuestionId).ToList(), instructor);
        }

        // يُرجع العنصر الأول بيد المدرب ليصبح Returned
        private static async Task ReturnFirstAsync(Harness h, Seeded s)
        {
            var r = await h.Service.ReturnItemAsync(s.Instructor.Id, s.ItemIds[0], "الصياغة غير واضحة وتحتاج مراجعة", Teacher);
            Assert.True(r.Success, r.Message);
        }

        // ───── 6.1 معالجة المرتجع ─────

        [Fact]
        public async Task Resolve_ApproveAsIs_ApprovesQuestion_ResolvesItem_AndAudits()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);
            await ReturnFirstAsync(h, s);

            var r = await h.Service.ResolveReturnedAsync(s.ItemIds[0], ReturnResolution.ApproveAsIs, null, Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var item = await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == s.ItemIds[0]);
            Assert.Equal(QuestionReviewTaskItemStatus.ReturnResolved, item.Status);
            Assert.False(item.IsLockActive);
            Assert.Equal("admin-1", item.ActionByUserId);

            var q = await db.Questions.SingleAsync(x => x.Id == s.QuestionIds[0]);
            Assert.True(q.IsReviewed);
            Assert.Equal("admin-1", q.ReviewedByUserId);

            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(0, task.ReturnedItems);
            Assert.Equal(1, await db.QuestionAuditLogs.CountAsync(a => a.Action == "معالجة مرتجع مهمة مراجعة"));
        }

        [Fact]
        public async Task Resolve_ReleaseToPool_KeepsQuestionUnreviewed_AndFreesHold()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);
            await ReturnFirstAsync(h, s);

            var locks = new QuestionReviewLockService(h.Factory);
            using (var before = h.Factory.CreateDbContext())
                Assert.Contains(s.QuestionIds[0], await locks.HeldQuestionIds(before).ToListAsync());

            var r = await h.Service.ResolveReturnedAsync(s.ItemIds[0], ReturnResolution.ReleaseToPool, "نعيده للمسبح", Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var q = await db.Questions.SingleAsync(x => x.Id == s.QuestionIds[0]);
            Assert.False(q.IsReviewed);
            Assert.False(q.IsRejected);
            Assert.DoesNotContain(s.QuestionIds[0], await locks.HeldQuestionIds(db).ToListAsync());

            var item = await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == s.ItemIds[0]);
            Assert.Equal("نعيده للمسبح", item.AdminResolutionNote);
        }

        [Fact]
        public async Task Resolve_Reject_MarksQuestionRejected()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);
            await ReturnFirstAsync(h, s);

            var r = await h.Service.ResolveReturnedAsync(s.ItemIds[0], ReturnResolution.Reject, null, Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            Assert.True((await db.Questions.SingleAsync(x => x.Id == s.QuestionIds[0])).IsRejected);
            Assert.Equal(QuestionReviewTaskItemStatus.ReturnResolved, (await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == s.ItemIds[0])).Status);
        }

        [Fact]
        public async Task Resolve_ItemNotReturned_Fails_AndChangesNothing()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);

            var r = await h.Service.ResolveReturnedAsync(s.ItemIds[0], ReturnResolution.Reject, null, Admin);

            Assert.False(r.Success);
            using var db = h.Factory.CreateDbContext();
            Assert.False((await db.Questions.SingleAsync(x => x.Id == s.QuestionIds[0])).IsRejected);
            Assert.Equal(QuestionReviewTaskItemStatus.Pending, (await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == s.ItemIds[0])).Status);
        }

        [Fact]
        public async Task Resolve_ApproveAsIs_UnapprovableQuestion_Fails_AndStaysReturned()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);
            await ReturnFirstAsync(h, s);
            using (var db0 = h.Factory.CreateDbContext())
            {
                var q0 = await db0.Questions.SingleAsync(x => x.Id == s.QuestionIds[0]);
                q0.CorrectAnswer = "لا يطابق أي خيار";
                await db0.SaveChangesAsync();
            }

            var r = await h.Service.ResolveReturnedAsync(s.ItemIds[0], ReturnResolution.ApproveAsIs, null, Admin);

            Assert.False(r.Success);
            using var db = h.Factory.CreateDbContext();
            Assert.False((await db.Questions.SingleAsync(x => x.Id == s.QuestionIds[0])).IsReviewed);
            Assert.Equal(QuestionReviewTaskItemStatus.Returned, (await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == s.ItemIds[0])).Status);
        }

        // ───── 6.2 إزالة عناصر ─────

        [Fact]
        public async Task RemoveItems_ReleasesLock_UpdatesCounters_AndKeepsAssignedWhilePendingRemain()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 4);

            var r = await h.Service.RemoveItemsAsync(s.TaskId, new[] { s.ItemIds[0], s.ItemIds[1] }, Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(QuestionReviewTaskStatus.Assigned, task.Status);   // الأدمن لا يبدأ المراجعة نيابةً عن المدرب
            Assert.Equal(2, task.RemovedItems);
            Assert.Equal(2, task.PendingItems);

            var removed = await db.QuestionReviewTaskItems.Where(i => i.Status == QuestionReviewTaskItemStatus.Removed).ToListAsync();
            Assert.Equal(2, removed.Count);
            Assert.All(removed, i => Assert.False(i.IsLockActive));
            Assert.Equal(2, await db.QuestionAuditLogs.CountAsync(a => a.Action == "إزالة من مهمة مراجعة"));
        }

        [Fact]
        public async Task RemoveItems_LastPending_CompletesTask()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 3);
            Assert.True((await h.Service.ApproveItemsAsync(s.Instructor.Id, s.TaskId, new[] { s.ItemIds[0] }, Teacher)).Success);

            var r = await h.Service.RemoveItemsAsync(s.TaskId, new[] { s.ItemIds[1], s.ItemIds[2] }, Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(QuestionReviewTaskStatus.Completed, task.Status);
            Assert.NotNull(task.CompletedAtUtc);
        }

        [Fact]
        public async Task RemoveItems_ApprovedItem_IsIgnored_AndRemovingEverythingIsRejected()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);
            Assert.True((await h.Service.ApproveItemsAsync(s.Instructor.Id, s.TaskId, new[] { s.ItemIds[0] }, Teacher)).Success);

            // المعتمد لا يُزال
            var approvedOnly = await h.Service.RemoveItemsAsync(s.TaskId, new[] { s.ItemIds[0] }, Admin);
            Assert.False(approvedOnly.Success);

            // إزالة آخر معلّق مع بقاء المعتمد مسموحة؛ أما إزالة كل الأسئلة الفعلية فلا
            var h2 = Create();
            var s2 = await SeedTaskAsync(h2, 2);
            var all = await h2.Service.RemoveItemsAsync(s2.TaskId, s2.ItemIds, Admin);
            Assert.False(all.Success);
            Assert.Contains("إلغاء المهمة", all.Message);
        }

        // ───── 6.2 إعادة الإسناد ─────

        [Fact]
        public async Task Reassign_MovesPendingToNewTask_WithParent_AndTransfersLock()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 4);
            Assert.True((await h.Service.ApproveItemsAsync(s.Instructor.Id, s.TaskId, new[] { s.ItemIds[0] }, Teacher)).Success);
            var other = await SeedInstructorAsync(h.Factory, "u-2", "أ. خالد");

            var r = await h.Service.ReassignRemainingAsync(s.TaskId, other.Id, Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var tasks = await db.QuestionReviewTasks.OrderBy(t => t.Id).ToListAsync();
            Assert.Equal(2, tasks.Count);
            var oldTask = tasks[0];
            var newTask = tasks[1];

            Assert.Equal("QRT-2026-0002", newTask.Code);
            Assert.Equal(oldTask.Id, newTask.ParentTaskId);
            Assert.Equal(other.Id, newTask.InstructorId);
            Assert.Equal(QuestionReviewTaskStatus.Assigned, newTask.Status);
            Assert.Equal(3, newTask.TotalItems);
            Assert.Equal(3, newTask.PendingItems);

            // المهمة القديمة: المعتمد بقي، والمعلّق أُزيل، وأُكملت
            Assert.Equal(QuestionReviewTaskStatus.Completed, oldTask.Status);
            Assert.Equal(1, oldTask.ApprovedItems);
            Assert.Equal(3, oldTask.RemovedItems);
            Assert.Equal(0, oldTask.PendingItems);

            // الحجز انتقل: 3 أقفال نشطة فقط وكلها على المهمة الجديدة، بلا تكرار لسؤال
            var activeLocks = await db.QuestionReviewTaskItems.Where(i => i.IsLockActive).ToListAsync();
            Assert.Equal(3, activeLocks.Count);
            Assert.All(activeLocks, i => Assert.Equal(newTask.Id, i.TaskId));
            Assert.Equal(3, activeLocks.Select(i => i.QuestionId).Distinct().Count());

            Assert.Equal(3, await db.QuestionAuditLogs.CountAsync(a => a.Action == "إعادة إسناد مهمة مراجعة"));

            h.Notifications.Verify(n => n.SendToUserAsync("u-2", It.Is<string>(m => m.Contains("QRT-2026-0002")),
                NotificationCategory.Important, It.IsAny<string?>()), Times.Once);
            h.Notifications.Verify(n => n.SendToUserAsync("u-1", It.Is<string>(m => m.Contains("سُحب") && m.Contains("QRT-2026-0001")),
                NotificationCategory.Important, It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task Reassign_WhenNothingHandled_CancelsOldTask()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);
            var other = await SeedInstructorAsync(h.Factory, "u-2", "أ. خالد");

            var r = await h.Service.ReassignRemainingAsync(s.TaskId, other.Id, Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var oldTask = await db.QuestionReviewTasks.OrderBy(t => t.Id).FirstAsync();
            Assert.Equal(QuestionReviewTaskStatus.Cancelled, oldTask.Status);
            Assert.Contains("QRT-2026-0002", oldTask.CancelReason);
        }

        [Fact]
        public async Task Reassign_ExpiredDue_IsNotCarriedOver()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2, dueLocal: new DateTime(2026, 10, 10, 12, 0, 0));
            using (var db0 = h.Factory.CreateDbContext())
            {
                var t0 = await db0.QuestionReviewTasks.SingleAsync();
                t0.DueAtUtc = Now.AddDays(-1);   // انقضى الموعد
                await db0.SaveChangesAsync();
            }
            var other = await SeedInstructorAsync(h.Factory, "u-2", "أ. خالد");

            var r = await h.Service.ReassignRemainingAsync(s.TaskId, other.Id, Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            Assert.Null((await db.QuestionReviewTasks.OrderBy(t => t.Id).LastAsync()).DueAtUtc);
        }

        [Fact]
        public async Task Reassign_Rejects_SameInstructor_PartnerMismatch_DeniedCurriculum_AndInactive()
        {
            var h = Create(id => id == 2 ? new List<int>() : new List<int> { 1 });
            var s = await SeedTaskAsync(h, 2);

            // نفس المدرب
            Assert.False((await h.Service.ReassignRemainingAsync(s.TaskId, s.Instructor.Id, Admin)).Success);

            // مدرب بلا صلاحية على المنهج (المعرّف 2 في هذا الـ Harness)
            var denied = await SeedInstructorAsync(h.Factory, "u-2", "أ. بلا منهج");
            Assert.Equal(2, denied.Id);
            var rDenied = await h.Service.ReassignRemainingAsync(s.TaskId, denied.Id, Admin);
            Assert.False(rDenied.Success);
            Assert.Contains("صلاحية", rDenied.Message);

            // مدرب بشريك مختلف (D7)
            var partner = await SeedInstructorAsync(h.Factory, "u-3", "أ. شريك", partnerId: 7);
            var rPartner = await h.Service.ReassignRemainingAsync(s.TaskId, partner.Id, Admin);
            Assert.False(rPartner.Success);
            Assert.Contains("الشريك", rPartner.Message);

            // مدرب غير نشط
            var inactive = await SeedInstructorAsync(h.Factory, "u-4", "أ. موقوف");
            using (var db0 = h.Factory.CreateDbContext())
            {
                (await db0.Instructors.SingleAsync(i => i.Id == inactive.Id)).IsActive = false;
                await db0.SaveChangesAsync();
            }
            Assert.False((await h.Service.ReassignRemainingAsync(s.TaskId, inactive.Id, Admin)).Success);

            // لم يتغير شيء
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(1, await db.QuestionReviewTasks.CountAsync());
            Assert.Equal(2, await db.QuestionReviewTaskItems.CountAsync(i => i.IsLockActive));
        }

        [Fact]
        public async Task GetReassignCandidates_ListsOnlyOtherEligibleInstructors_AndFailsForFinalTask()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);
            var other = await SeedInstructorAsync(h.Factory, "u-2", "أ. خالد");
            var noCurriculum = await SeedInstructorAsync(h.Factory, "u-3", "أ. بلا منهج");

            // الأهلية تُحسب من الربط المباشر الفعّال: المدرب الحالي والآخر فقط
            using (var db0 = h.Factory.CreateDbContext())
            {
                db0.Batches.Add(new Batch { Id = 1, Name = "دفعة اختبار", IsActive = true, IsDeleted = false, IsArchived = false });
                foreach (var ins in new[] { s.Instructor, other })
                    db0.InstructorCurriculumBatches.Add(new InstructorCurriculumBatch { InstructorId = ins.Id, CurriculumId = 1, BatchId = 1, UserId = "x" });
                await db0.SaveChangesAsync();
            }

            var r = await h.Service.GetReassignCandidatesAsync(s.TaskId);
            Assert.True(r.Success, r.Message);
            var data = r.Data!;
            var instructors = ((IEnumerable<EligibleInstructorDto>)data.GetType().GetProperty("instructors")!.GetValue(data)!).ToList();
            Assert.Single(instructors);
            Assert.Equal(other.Id, instructors[0].Id);
            Assert.DoesNotContain(instructors, i => i.Id == noCurriculum.Id || i.Id == s.Instructor.Id);

            Assert.True((await h.Service.CancelTaskAsync(s.TaskId, "إلغاء للاختبار", Admin)).Success);
            Assert.False((await h.Service.GetReassignCandidatesAsync(s.TaskId)).Success);
        }

        // ───── 6.3 إلغاء / إغلاق / تمديد ─────

        [Fact]
        public async Task Cancel_RemovesPending_KeepsReturned_SetsCancelled_AndNotifiesInstructor()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 3);
            await ReturnFirstAsync(h, s);

            var r = await h.Service.CancelTaskAsync(s.TaskId, "تغيّرت أولويات الدفعة", Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(QuestionReviewTaskStatus.Cancelled, task.Status);
            Assert.Equal("تغيّرت أولويات الدفعة", task.CancelReason);
            Assert.NotNull(task.CancelledAtUtc);
            Assert.Equal(2, task.RemovedItems);
            Assert.Equal(1, task.ReturnedItems);   // المرتجع يبقى للأدمن

            Assert.Equal(0, await db.QuestionReviewTaskItems.CountAsync(i => i.IsLockActive));
            Assert.Equal(2, await db.QuestionAuditLogs.CountAsync(a => a.Action == "إلغاء مهمة مراجعة"));
            h.Notifications.Verify(n => n.SendToUserAsync("u-1", It.Is<string>(m => m.Contains("تغيّرت أولويات الدفعة")),
                NotificationCategory.Important, It.IsAny<string?>()), Times.Once);

            // المرتجع ما زال قابلًا للمعالجة بعد الإلغاء
            Assert.True((await h.Service.ResolveReturnedAsync(s.ItemIds[0], ReturnResolution.ReleaseToPool, null, Admin)).Success);
        }

        [Fact]
        public async Task Cancel_RequiresReason_AndFinalTasksCannotBeCancelled()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);

            Assert.False((await h.Service.CancelTaskAsync(s.TaskId, "  ", Admin)).Success);
            Assert.False((await h.Service.CancelTaskAsync(s.TaskId, "قصير", Admin)).Success);

            Assert.True((await h.Service.CancelTaskAsync(s.TaskId, "سبب كافٍ للإلغاء", Admin)).Success);
            Assert.False((await h.Service.CancelTaskAsync(s.TaskId, "سبب كافٍ للإلغاء", Admin)).Success);
        }

        [Fact]
        public async Task Close_RequiresCompleted_AndNoUnresolvedReturns()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);

            // لم تكتمل بعد
            Assert.False((await h.Service.CloseTaskAsync(s.TaskId, Admin)).Success);

            // اكتملت لكن بها مرتجع
            await ReturnFirstAsync(h, s);
            Assert.True((await h.Service.ApproveItemsAsync(s.Instructor.Id, s.TaskId, new[] { s.ItemIds[1] }, Teacher)).Success);
            var blocked = await h.Service.CloseTaskAsync(s.TaskId, Admin);
            Assert.False(blocked.Success);
            Assert.Contains("مرتجع", blocked.Message);

            // بعد المعالجة يُغلق
            Assert.True((await h.Service.ResolveReturnedAsync(s.ItemIds[0], ReturnResolution.ApproveAsIs, null, Admin)).Success);
            var closed = await h.Service.CloseTaskAsync(s.TaskId, Admin);
            Assert.True(closed.Success, closed.Message);

            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(QuestionReviewTaskStatus.Closed, task.Status);
            Assert.NotNull(task.ClosedAtUtc);

            // نهائية: لا إلغاء ولا إغلاق ثانٍ
            Assert.False((await h.Service.CloseTaskAsync(s.TaskId, Admin)).Success);
            Assert.False((await h.Service.CancelTaskAsync(s.TaskId, "بعد الإغلاق لا يجوز", Admin)).Success);
        }

        [Fact]
        public async Task ExtendDue_ValidatesFuture_ResetsReminder_AndAllowsClearing()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);
            using (var db0 = h.Factory.CreateDbContext())
            {
                (await db0.QuestionReviewTasks.SingleAsync()).LastReminderAtUtc = Now.AddHours(-3);
                await db0.SaveChangesAsync();
            }

            Assert.False((await h.Service.ExtendDueAsync(s.TaskId, Now.AddMinutes(-1), Admin)).Success);

            var newDue = Now.AddDays(5);
            var r = await h.Service.ExtendDueAsync(s.TaskId, newDue, Admin);
            Assert.True(r.Success, r.Message);
            using (var db = h.Factory.CreateDbContext())
            {
                var task = await db.QuestionReviewTasks.SingleAsync();
                Assert.Equal(newDue, task.DueAtUtc);
                Assert.Null(task.LastReminderAtUtc);
            }
            h.Notifications.Verify(n => n.SendToUserAsync("u-1", It.Is<string>(m => m.Contains("عُدّل موعد")),
                NotificationCategory.Important, It.Is<string?>(u => u != null && u.Contains("/Instructors/QuestionReviewTasks/Review/"))), Times.Once);

            Assert.True((await h.Service.ExtendDueAsync(s.TaskId, null, Admin)).Success);
            using var db2 = h.Factory.CreateDbContext();
            Assert.Null((await db2.QuestionReviewTasks.SingleAsync()).DueAtUtc);
        }

        [Fact]
        public async Task ExtendDue_OnFinalTask_Fails()
        {
            var h = Create();
            var s = await SeedTaskAsync(h, 2);
            Assert.True((await h.Service.CancelTaskAsync(s.TaskId, "سبب كافٍ للإلغاء", Admin)).Success);

            Assert.False((await h.Service.ExtendDueAsync(s.TaskId, Now.AddDays(2), Admin)).Success);
        }

        // ───── قواعد الحالة ─────

        [Theory]
        [InlineData(QuestionReviewTaskStatus.Assigned, true)]
        [InlineData(QuestionReviewTaskStatus.InProgress, true)]
        [InlineData(QuestionReviewTaskStatus.Completed, true)]
        [InlineData(QuestionReviewTaskStatus.Closed, false)]
        [InlineData(QuestionReviewTaskStatus.Cancelled, false)]
        public void StateRules_Cancel_AllowedUntilFinal(QuestionReviewTaskStatus status, bool expected)
            => Assert.Equal(expected, QuestionReviewTaskStateRules.CanCancel(status));

        [Theory]
        [InlineData(QuestionReviewTaskStatus.Completed, 0, true)]
        [InlineData(QuestionReviewTaskStatus.Completed, 1, false)]
        [InlineData(QuestionReviewTaskStatus.InProgress, 0, false)]
        [InlineData(QuestionReviewTaskStatus.Closed, 0, false)]
        public void StateRules_Close_OnlyCompletedWithoutReturns(QuestionReviewTaskStatus status, int returned, bool expected)
            => Assert.Equal(expected, QuestionReviewTaskStateRules.CanClose(status, returned));

        [Theory]
        [InlineData(QuestionReviewTaskStatus.Assigned, 3, true)]
        [InlineData(QuestionReviewTaskStatus.InProgress, 0, false)]
        [InlineData(QuestionReviewTaskStatus.Completed, 2, false)]
        [InlineData(QuestionReviewTaskStatus.Cancelled, 2, false)]
        public void StateRules_RemoveAndReassign_NeedActiveTaskWithPending(QuestionReviewTaskStatus status, int pending, bool expected)
        {
            Assert.Equal(expected, QuestionReviewTaskStateRules.CanRemoveItems(status, pending));
            Assert.Equal(expected, QuestionReviewTaskStateRules.CanReassign(status, pending));
        }
    }
}
