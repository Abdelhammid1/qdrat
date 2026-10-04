using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    // QRT-S3 — مراجعة المدرب: اعتماد فردي/جماعي، إرجاع بملاحظة، انتقالات الحالة، IDOR، القراءات
    public class QuestionReviewTaskInstructorTests
    {
        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;
            public FixedTimeProvider(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private sealed class Harness
        {
            public required QuestionReviewTaskService Service { get; init; }
            public required QuestionReviewTaskQueryService Query { get; init; }
            public required TestDbContextFactory Factory { get; init; }
            public required Mock<IAdvancedNotificationService> Notifications { get; init; }
        }

        private static readonly ReviewActor Admin = new("admin-1", "مدير النظام", UserRoleType.Admin);
        private static readonly ReviewActor Teacher = new("u-1", "أ. محمد", UserRoleType.Instructor);
        private static readonly ReviewActor OtherTeacher = new("u-2", "أ. خالد", UserRoleType.Instructor);

        private static Harness Create()
        {
            var factory = new TestDbContextFactory(Guid.NewGuid().ToString());
            var scope = new Mock<IInstructorScopeService>();
            scope.Setup(s => s.GetDirectCurriculumIdsAsync(It.IsAny<int>())).ReturnsAsync(new List<int> { 1 });
            var notifications = new Mock<IAdvancedNotificationService>();
            var time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 4, 10, 0, 0, TimeSpan.Zero));

            return new Harness
            {
                Service = new QuestionReviewTaskService(factory, time, scope.Object, notifications.Object,
                    NullLogger<QuestionReviewTaskService>.Instance),
                Query = new QuestionReviewTaskQueryService(factory, time, new MemoryCache(new MemoryCacheOptions())),
                Factory = factory,
                Notifications = notifications
            };
        }

        private static async Task<Instructor> SeedInstructorAsync(TestDbContextFactory factory, string userId, string name)
        {
            using var db = factory.CreateDbContext();
            var i = new Instructor
            {
                FullName = name,
                NationalID = Guid.NewGuid().ToString("N")[..10],
                Email = $"{Guid.NewGuid():N}@test.local",
                Specialization = "عام",
                IsActive = true,
                UserId = userId
            };
            db.Instructors.Add(i);
            await db.SaveChangesAsync();
            return i;
        }

        // أسئلة صالحة للاعتماد: لها خيار نصه = الإجابة الصحيحة
        private static async Task<List<Question>> SeedQuestionsAsync(
            TestDbContextFactory factory, int count, Action<Question, int>? customize = null)
        {
            using var db = factory.CreateDbContext();

            // الهرم الثابت Curriculum → Section → Lesson (InMemory يستبعد الصفوف التي تفتقد علاقة إلزامية)
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
                    Options = new List<QuestionOption>
                    {
                        new() { Text = "أ" }, new() { Text = "ب" }
                    }
                };
                customize?.Invoke(q, i);
                list.Add(q);
            }
            db.Questions.AddRange(list);
            await db.SaveChangesAsync();
            return list;
        }

        private static async Task<(int TaskId, List<long> ItemIds, Instructor Instructor)> SeedTaskAsync(
            Harness h, int count, Action<Question, int>? customize = null)
        {
            var instructor = await SeedInstructorAsync(h.Factory, "u-1", "أ. محمد");
            var questions = await SeedQuestionsAsync(h.Factory, count, customize);
            var created = await h.Service.CreateTaskAsync(new CreateQuestionReviewTaskInput
            {
                Title = "مراجعة الجبر",
                InstructorId = instructor.Id,
                SelectedQuestionIds = questions.Select(q => q.Id).ToList()
            }, Admin);
            Assert.True(created.Success, created.Message);

            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            var ids = await db.QuestionReviewTaskItems.OrderBy(i => i.SortOrder).Select(i => i.Id).ToListAsync();
            return (task.Id, ids, instructor);
        }

        // ───── اعتماد ─────

        [Fact]
        public async Task Approve_ValidItems_MarksQuestionAndItem_AuditsAndMovesToInProgress()
        {
            var h = Create();
            var (taskId, ids, ins) = await SeedTaskAsync(h, 3);

            var r = await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { ids[0], ids[1] }, Teacher);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(QuestionReviewTaskStatus.InProgress, task.Status);
            Assert.NotNull(task.StartedAtUtc);
            Assert.Equal(2, task.ApprovedItems);
            Assert.Equal(1, task.PendingItems);

            var approved = await db.QuestionReviewTaskItems.Where(i => i.Status == QuestionReviewTaskItemStatus.Approved).ToListAsync();
            Assert.Equal(2, approved.Count);
            Assert.All(approved, i =>
            {
                Assert.False(i.IsLockActive);
                Assert.Equal("u-1", i.ActionByUserId);
                Assert.NotNull(i.ActionAtUtc);
            });

            Assert.Equal(2, await db.Questions.CountAsync(q => q.IsReviewed && q.ReviewedByUserId == "u-1" && q.ReviewedAt != null));
            var audits = await db.QuestionAuditLogs.Where(a => a.Action == "اعتماد ضمن مهمة مراجعة").ToListAsync();
            Assert.Equal(2, audits.Count);
            Assert.All(audits, a => Assert.Contains("QRT-2026-0001", a.ChangedFieldsSummary));

            // لم تكتمل بعد: لا إشعار إنجاز للأدمن
            h.Notifications.Verify(n => n.SendToUserAsync("admin-1", It.IsAny<string>(), It.IsAny<NotificationCategory>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Approve_LastPendingItems_CompletesTask_AndNotifiesAdminWithPercent()
        {
            var h = Create();
            var (taskId, ids, ins) = await SeedTaskAsync(h, 2);

            var r = await h.Service.ApproveItemsAsync(ins.Id, taskId, ids, Teacher);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(QuestionReviewTaskStatus.Completed, task.Status);
            Assert.NotNull(task.CompletedAtUtc);
            Assert.Equal(0, task.PendingItems);

            h.Notifications.Verify(n => n.SendToUserAsync(
                "admin-1", It.Is<string>(m => m.Contains("QRT-2026-0001") && m.Contains("100%")),
                NotificationCategory.Important, It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task Approve_InvalidQuestion_IsSkipped_AndReported()
        {
            var h = Create();
            // السؤال 1: إجابته الصحيحة لا تطابق أي خيار
            var (taskId, ids, ins) = await SeedTaskAsync(h, 2, (q, i) => { if (i == 1) q.CorrectAnswer = "غير موجود"; });

            var r = await h.Service.ApproveItemsAsync(ins.Id, taskId, ids, Teacher);

            Assert.True(r.Success, r.Message);
            Assert.Contains("تم اعتماد 1", r.Message);
            Assert.Contains("تعذّر اعتماد 1", r.Message);
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(1, await db.QuestionReviewTaskItems.CountAsync(i => i.Status == QuestionReviewTaskItemStatus.Pending));
            Assert.Equal(QuestionReviewTaskStatus.InProgress, (await db.QuestionReviewTasks.SingleAsync()).Status);
        }

        [Fact]
        public async Task Approve_NothingApprovable_FailsWithoutChanges()
        {
            var h = Create();
            var (taskId, ids, ins) = await SeedTaskAsync(h, 1, (q, i) => q.CorrectAnswer = "غير موجود");

            var r = await h.Service.ApproveItemsAsync(ins.Id, taskId, ids, Teacher);

            Assert.False(r.Success);
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(QuestionReviewTaskStatus.Assigned, (await db.QuestionReviewTasks.SingleAsync()).Status);
            Assert.Equal(0, await db.Questions.CountAsync(q => q.IsReviewed));
        }

        [Fact]
        public async Task Approve_ByAnotherInstructor_IsRejected_NoChange()
        {
            var h = Create();
            var (taskId, ids, _) = await SeedTaskAsync(h, 2);
            var other = await SeedInstructorAsync(h.Factory, "u-2", "أ. خالد");

            var r = await h.Service.ApproveItemsAsync(other.Id, taskId, ids, OtherTeacher);

            Assert.False(r.Success);
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(0, await db.Questions.CountAsync(q => q.IsReviewed));
            Assert.Equal(2, await db.QuestionReviewTaskItems.CountAsync(i => i.Status == QuestionReviewTaskItemStatus.Pending && i.IsLockActive));
        }

        [Fact]
        public async Task Approve_ItemIdsFromAnotherTask_AreIgnored()
        {
            var h = Create();
            var (taskId, ids, ins) = await SeedTaskAsync(h, 1);
            var foreignQuestions = await SeedQuestionsAsync(h.Factory, 1);
            var second = await h.Service.CreateTaskAsync(new CreateQuestionReviewTaskInput
            {
                Title = "مهمة ثانية",
                InstructorId = ins.Id,
                SelectedQuestionIds = foreignQuestions.Select(q => q.Id).ToList()
            }, Admin);
            Assert.True(second.Success, second.Message);

            using (var db0 = h.Factory.CreateDbContext())
            {
                var foreignItem = await db0.QuestionReviewTaskItems.OrderByDescending(i => i.Id).FirstAsync();
                var r = await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { foreignItem.Id }, Teacher);
                Assert.False(r.Success);
            }

            using var db = h.Factory.CreateDbContext();
            Assert.Equal(0, await db.Questions.CountAsync(q => q.IsReviewed));
        }

        [Fact]
        public async Task Approve_OnCancelledOrClosedTask_Fails()
        {
            var h = Create();
            var (taskId, ids, ins) = await SeedTaskAsync(h, 1);
            using (var db = h.Factory.CreateDbContext())
            {
                var t = await db.QuestionReviewTasks.SingleAsync();
                t.Status = QuestionReviewTaskStatus.Cancelled;
                await db.SaveChangesAsync();
            }

            var r = await h.Service.ApproveItemsAsync(ins.Id, taskId, ids, Teacher);

            Assert.False(r.Success);
        }

        [Fact]
        public async Task Approve_EmptySelection_Fails()
        {
            var h = Create();
            var (taskId, _, ins) = await SeedTaskAsync(h, 1);

            Assert.False((await h.Service.ApproveItemsAsync(ins.Id, taskId, Array.Empty<long>(), Teacher)).Success);
        }

        // ───── إرجاع ─────

        [Fact]
        public async Task Return_RequiresNoteBetween10And500()
        {
            var h = Create();
            var (_, ids, ins) = await SeedTaskAsync(h, 1);

            Assert.False((await h.Service.ReturnItemAsync(ins.Id, ids[0], "قصير", Teacher)).Success);
            Assert.False((await h.Service.ReturnItemAsync(ins.Id, ids[0], "          ", Teacher)).Success);
            Assert.False((await h.Service.ReturnItemAsync(ins.Id, ids[0], new string('م', 501), Teacher)).Success);

            using var db = h.Factory.CreateDbContext();
            Assert.Equal(QuestionReviewTaskItemStatus.Pending, (await db.QuestionReviewTaskItems.SingleAsync()).Status);
        }

        [Fact]
        public async Task Return_SetsReturned_ReleasesLock_Audits_AndCompletesWhenNothingPending()
        {
            var h = Create();
            var (taskId, ids, ins) = await SeedTaskAsync(h, 1);

            var r = await h.Service.ReturnItemAsync(ins.Id, ids[0], "الإجابة الصحيحة المحددة غير صحيحة.", Teacher);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var item = await db.QuestionReviewTaskItems.SingleAsync();
            Assert.Equal(QuestionReviewTaskItemStatus.Returned, item.Status);
            Assert.False(item.IsLockActive);
            Assert.Equal("الإجابة الصحيحة المحددة غير صحيحة.", item.ReturnNote);

            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(1, task.ReturnedItems);
            Assert.Equal(0, task.PendingItems);
            Assert.Equal(0, task.ApprovedItems);
            Assert.Equal(QuestionReviewTaskStatus.Completed, task.Status);

            // الإرجاع لا يعتمد السؤال ولا يرفضه
            var q = await db.Questions.SingleAsync();
            Assert.False(q.IsReviewed);
            Assert.False(q.IsRejected);
            Assert.Equal(1, await db.QuestionAuditLogs.CountAsync(a => a.Action == "إرجاع من مهمة مراجعة" && a.ChangedFieldsSummary!.Contains("الإجابة")));

            h.Notifications.Verify(n => n.SendToUserAsync(
                "admin-1", It.Is<string>(m => m.Contains("0%")), NotificationCategory.Important, It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task Return_ByAnotherInstructor_IsRejected()
        {
            var h = Create();
            var (_, ids, _) = await SeedTaskAsync(h, 1);
            var other = await SeedInstructorAsync(h.Factory, "u-2", "أ. خالد");

            var r = await h.Service.ReturnItemAsync(other.Id, ids[0], "ملاحظة طويلة بما يكفي.", OtherTeacher);

            Assert.False(r.Success);
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(QuestionReviewTaskItemStatus.Pending, (await db.QuestionReviewTaskItems.SingleAsync()).Status);
        }

        [Fact]
        public async Task Return_AlreadyHandledItem_Fails()
        {
            var h = Create();
            var (taskId, ids, ins) = await SeedTaskAsync(h, 2);
            Assert.True((await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { ids[0] }, Teacher)).Success);

            var r = await h.Service.ReturnItemAsync(ins.Id, ids[0], "ملاحظة طويلة بما يكفي.", Teacher);

            Assert.False(r.Success);
        }

        // ───── القراءات ─────

        [Fact]
        public async Task Query_ReviewVm_IsNullForAnotherInstructor_AndHasProgressForOwner()
        {
            var h = Create();
            var (taskId, ids, ins) = await SeedTaskAsync(h, 4);
            var other = await SeedInstructorAsync(h.Factory, "u-2", "أ. خالد");
            await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { ids[0], ids[1], ids[2] }, Teacher);
            await h.Service.ReturnItemAsync(ins.Id, ids[3], "ملاحظة طويلة بما يكفي.", Teacher);

            Assert.Null(await h.Query.GetInstructorTaskReviewAsync(other.Id, taskId));

            var vm = await h.Query.GetInstructorTaskReviewAsync(ins.Id, taskId);
            Assert.NotNull(vm);
            Assert.Equal(75, vm!.Progress.ApprovalPercent);
            Assert.Equal(100, vm.Progress.HandledPercent);
            Assert.False(vm.CanAct); // اكتملت
        }

        [Fact]
        public async Task Query_ItemsPage_ChecksOwnership_PagesAndFiltersByStatus()
        {
            var h = Create();
            var (taskId, ids, ins) = await SeedTaskAsync(h, 5);
            var other = await SeedInstructorAsync(h.Factory, "u-2", "أ. خالد");
            await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { ids[0], ids[1] }, Teacher);

            Assert.Null(await h.Query.GetItemsPageAsync(other.Id, taskId, 0, 25, null, null));

            var all = await h.Query.GetItemsPageAsync(ins.Id, taskId, 0, 2, null, null);
            Assert.Equal(5, all!.Total);
            Assert.Equal(5, all.Filtered);
            Assert.Equal(2, all.Rows.Count);

            var pending = await h.Query.GetItemsPageAsync(ins.Id, taskId, 0, 25, (int)QuestionReviewTaskItemStatus.Pending, null);
            Assert.Equal(3, pending!.Filtered);
            Assert.All(pending.Rows, r => Assert.True(r.CanAct));

            var approved = await h.Query.GetItemsPageAsync(ins.Id, taskId, 0, 25, (int)QuestionReviewTaskItemStatus.Approved, null);
            Assert.All(approved!.Rows, r => Assert.False(r.CanAct));
        }

        [Fact]
        public async Task Query_ItemsPage_CapsPageSizeAt100()
        {
            var h = Create();
            var (taskId, _, ins) = await SeedTaskAsync(h, 120);

            var page = await h.Query.GetItemsPageAsync(ins.Id, taskId, 0, 5000, null, null);

            Assert.Equal(100, page!.Rows.Count);
            Assert.Equal(120, page.Total);
        }

        [Fact]
        public async Task Query_PreviewQuestion_OnlyForOwner()
        {
            var h = Create();
            var (_, ids, ins) = await SeedTaskAsync(h, 1);
            var other = await SeedInstructorAsync(h.Factory, "u-2", "أ. خالد");

            Assert.Null(await h.Query.GetPreviewQuestionAsync(other.Id, ids[0]));
            var q = await h.Query.GetPreviewQuestionAsync(ins.Id, ids[0]);
            Assert.NotNull(q);
            Assert.Equal(2, q!.Options.Count);
        }

        [Fact]
        public async Task Query_Index_SortsActiveFirst_AndCountsPending()
        {
            var h = Create();
            var (taskId, ids, ins) = await SeedTaskAsync(h, 3);
            await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { ids[0] }, Teacher);

            var vm = await h.Query.GetInstructorTasksAsync(ins.Id, null);
            Assert.Single(vm.Tasks);
            Assert.Equal(1, vm.ActiveCount);
            Assert.Equal(2, vm.PendingQuestions);

            var none = await h.Query.GetInstructorTasksAsync(ins.Id, QuestionReviewTaskStatus.Closed);
            Assert.Empty(none.Tasks);
            Assert.Equal(1, none.ActiveCount); // الأعداد الإجمالية لا تتأثر بالفلتر
        }

        [Fact]
        public async Task Query_PendingBadge_CountsActiveTasksOnly_AndInvalidates()
        {
            var h = Create();
            var (taskId, ids, ins) = await SeedTaskAsync(h, 3);

            Assert.Equal(3, await h.Query.GetPendingCountByUserIdAsync("u-1"));

            await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { ids[0] }, Teacher);
            Assert.Equal(3, await h.Query.GetPendingCountByUserIdAsync("u-1")); // ما زال مخزَّنًا
            h.Query.InvalidatePendingCount("u-1");
            Assert.Equal(2, await h.Query.GetPendingCountByUserIdAsync("u-1"));
            Assert.Equal(0, await h.Query.GetPendingCountByUserIdAsync("nobody"));
        }

        [Fact]
        public void Metrics_Percentages_HandleZeroAndRounding()
        {
            Assert.Equal(0, QuestionReviewTaskMetrics.ApprovalPercent(0, 0));
            Assert.Equal(67, QuestionReviewTaskMetrics.ApprovalPercent(2, 3));
            Assert.Equal(100, QuestionReviewTaskMetrics.HandledPercent(0, 5));
            Assert.Equal(4, QuestionReviewTaskMetrics.Effective(5, 1));
        }
    }
}
