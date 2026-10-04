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
    // QRT-S4 — الدمج مع المسارات الحالية: تعديل واعتماد داخل المهمة، حماية المسار العام، مزامنة قرارات الأدمن، IDOR
    public class QuestionReviewTaskIntegrationTests
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
            public required QuestionReviewLockService Locks { get; init; }
            public required TestDbContextFactory Factory { get; init; }
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
                Locks = new QuestionReviewLockService(factory),
                Factory = factory
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

        private static async Task<List<Question>> SeedQuestionsAsync(TestDbContextFactory factory, int count)
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
                list.Add(new Question
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
                });
            }
            db.Questions.AddRange(list);
            await db.SaveChangesAsync();
            return list;
        }

        private static async Task<(int TaskId, List<long> ItemIds, List<Guid> QuestionIds, Instructor Instructor)> SeedTaskAsync(Harness h, int count)
        {
            var instructor = await SeedInstructorAsync(h.Factory, "u-1", "أ. محمد");
            var questions = await SeedQuestionsAsync(h.Factory, count);
            var created = await h.Service.CreateTaskAsync(new CreateQuestionReviewTaskInput
            {
                Title = "مراجعة الجبر",
                InstructorId = instructor.Id,
                SelectedQuestionIds = questions.Select(q => q.Id).ToList()
            }, Admin);
            Assert.True(created.Success, created.Message);

            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            var items = await db.QuestionReviewTaskItems.OrderBy(i => i.SortOrder).Select(i => new { i.Id, i.QuestionId }).ToListAsync();
            return (task.Id, items.Select(i => i.Id).ToList(), items.Select(i => i.QuestionId).ToList(), instructor);
        }

        // يحاكي ما يفعله مسار تعديل المدرب: يحفظ السؤال معتمدًا بيد المدرب
        private static async Task ApproveQuestionAsync(Harness h, Guid questionId, string byUserId)
        {
            using var db = h.Factory.CreateDbContext();
            var q = await db.Questions.SingleAsync(x => x.Id == questionId);
            q.IsReviewed = true;
            q.ReviewedByUserId = byUserId;
            q.ReviewedAt = DateTime.Now;
            await db.SaveChangesAsync();
        }

        private static async Task SetQuestionAsync(Harness h, Guid questionId, Action<Question> change)
        {
            using var db = h.Factory.CreateDbContext();
            var q = await db.Questions.SingleAsync(x => x.Id == questionId);
            change(q);
            await db.SaveChangesAsync();
        }

        // ───── S4.1: تعديل واعتماد داخل المهمة ─────

        [Fact]
        public async Task MarkEditedAndApproved_AfterQuestionApprovedByInstructor_UpdatesItemCountersAndAudit()
        {
            var h = Create();
            var (taskId, ids, qids, ins) = await SeedTaskAsync(h, 2);
            await ApproveQuestionAsync(h, qids[0], Teacher.UserId);

            var result = await h.Service.MarkEditedAndApprovedAsync(ins.Id, ids[0], Teacher);

            Assert.True(result.Success, result.Message);
            using var db = h.Factory.CreateDbContext();
            var item = await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == ids[0]);
            Assert.Equal(QuestionReviewTaskItemStatus.EditedAndApproved, item.Status);
            Assert.False(item.IsLockActive);
            Assert.Equal("u-1", item.ActionByUserId);
            Assert.NotNull(item.ActionAtUtc);

            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(QuestionReviewTaskStatus.InProgress, task.Status);
            Assert.Equal(1, task.ApprovedItems);
            Assert.Equal(1, task.PendingItems);
            Assert.Equal(1, await db.QuestionAuditLogs.CountAsync(a => a.QuestionId == qids[0] && a.Action == "اعتماد ضمن مهمة مراجعة"));
        }

        [Fact]
        public async Task MarkEditedAndApproved_LastPendingItem_CompletesTask()
        {
            var h = Create();
            var (_, ids, qids, ins) = await SeedTaskAsync(h, 1);
            await ApproveQuestionAsync(h, qids[0], Teacher.UserId);

            var result = await h.Service.MarkEditedAndApprovedAsync(ins.Id, ids[0], Teacher);

            Assert.True(result.Success, result.Message);
            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(QuestionReviewTaskStatus.Completed, task.Status);
            Assert.NotNull(task.CompletedAtUtc);
        }

        [Fact]
        public async Task MarkEditedAndApproved_QuestionNotApproved_FailsAndKeepsItemPending()
        {
            var h = Create();
            var (_, ids, _, ins) = await SeedTaskAsync(h, 1);

            var result = await h.Service.MarkEditedAndApprovedAsync(ins.Id, ids[0], Teacher);

            Assert.False(result.Success);
            using var db = h.Factory.CreateDbContext();
            var item = await db.QuestionReviewTaskItems.SingleAsync();
            Assert.Equal(QuestionReviewTaskItemStatus.Pending, item.Status);
            Assert.True(item.IsLockActive);
        }

        [Fact]
        public async Task MarkEditedAndApproved_QuestionApprovedByAnotherUser_Fails()
        {
            var h = Create();
            var (_, ids, qids, ins) = await SeedTaskAsync(h, 1);
            await ApproveQuestionAsync(h, qids[0], "someone-else");

            var result = await h.Service.MarkEditedAndApprovedAsync(ins.Id, ids[0], Teacher);

            Assert.False(result.Success);
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(QuestionReviewTaskItemStatus.Pending, (await db.QuestionReviewTaskItems.SingleAsync()).Status);
        }

        [Fact]
        public async Task MarkEditedAndApproved_OtherInstructor_Fails_NoIdor()
        {
            var h = Create();
            var (_, ids, qids, _) = await SeedTaskAsync(h, 1);
            var other = await SeedInstructorAsync(h.Factory, "u-2", "أ. خالد");
            await ApproveQuestionAsync(h, qids[0], OtherTeacher.UserId);

            var result = await h.Service.MarkEditedAndApprovedAsync(other.Id, ids[0], OtherTeacher);

            Assert.False(result.Success);
            using var db = h.Factory.CreateDbContext();
            var item = await db.QuestionReviewTaskItems.SingleAsync();
            Assert.Equal(QuestionReviewTaskItemStatus.Pending, item.Status);
            Assert.True(item.IsLockActive);
        }

        [Fact]
        public async Task GetEditableItem_OwnerPending_ReturnsItem_OthersAndHandledReturnNull()
        {
            var h = Create();
            var (taskId, ids, qids, ins) = await SeedTaskAsync(h, 2);
            var other = await SeedInstructorAsync(h.Factory, "u-2", "أ. خالد");

            var mine = await h.Query.GetEditableItemAsync(ins.Id, ids[0]);
            Assert.NotNull(mine);
            Assert.Equal(qids[0], mine!.QuestionId);
            Assert.Equal(taskId, mine.TaskId);

            Assert.Null(await h.Query.GetEditableItemAsync(other.Id, ids[0]));      // مدرب آخر
            Assert.Null(await h.Query.GetEditableItemAsync(ins.Id, 999_999));         // غير موجود

            await h.Service.ReturnItemAsync(ins.Id, ids[1], "الإجابة الصحيحة غير صحيحة.", Teacher);
            Assert.Null(await h.Query.GetEditableItemAsync(ins.Id, ids[1]));          // لم يعد معلّقًا
        }

        // ───── S4.2: حماية المسار العام ─────

        [Fact]
        public async Task Hold_PendingItem_IsHeldFromPublicPath()
        {
            var h = Create();
            var (taskId, ids, qids, ins) = await SeedTaskAsync(h, 2);

            var hold = await h.Locks.GetHoldAsync(qids[0]);

            Assert.NotNull(hold);
            Assert.Equal(QuestionReviewTaskItemStatus.Pending, hold!.ItemStatus);
            Assert.Equal(taskId, hold.TaskId);
            Assert.Equal(ins.Id, hold.InstructorId);

            using var db = h.Factory.CreateDbContext();
            var held = await h.Locks.HeldQuestionIds(db).ToListAsync();
            Assert.Equal(2, held.Count);
        }

        [Fact]
        public async Task Hold_ReturnedItem_StaysHeldUntilAdminResolves()
        {
            var h = Create();
            var (_, ids, qids, ins) = await SeedTaskAsync(h, 2);
            await h.Service.ReturnItemAsync(ins.Id, ids[0], "صياغة غير واضحة وتحتاج إعادة كتابة.", Teacher);

            var hold = await h.Locks.GetHoldAsync(qids[0]);

            Assert.NotNull(hold);
            Assert.Equal(QuestionReviewTaskItemStatus.Returned, hold!.ItemStatus);
            using var db = h.Factory.CreateDbContext();
            Assert.Contains(qids[0], await h.Locks.HeldQuestionIds(db).ToListAsync());
        }

        [Fact]
        public async Task Hold_ApprovedItem_IsReleased()
        {
            var h = Create();
            var (taskId, ids, qids, ins) = await SeedTaskAsync(h, 2);
            await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { ids[0] }, Teacher);

            Assert.Null(await h.Locks.GetHoldAsync(qids[0]));
            Assert.NotNull(await h.Locks.GetHoldAsync(qids[1]));
        }

        // ───── S4.3: مزامنة قرارات الأدمن ─────

        [Fact]
        public async Task Sync_AdminApprovedPendingQuestion_MarksItemApprovedByAdminAndReleasesLock()
        {
            var h = Create();
            var (_, ids, qids, _) = await SeedTaskAsync(h, 2);
            await SetQuestionAsync(h, qids[0], q => { q.IsReviewed = true; q.ReviewedByUserId = Admin.UserId; });

            var affected = await h.Service.SyncAdminDecisionAsync(AdminQuestionDecision.Approved, Admin);

            Assert.Equal(1, affected);
            using var db = h.Factory.CreateDbContext();
            var item = await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == ids[0]);
            Assert.Equal(QuestionReviewTaskItemStatus.ApprovedByAdmin, item.Status);
            Assert.False(item.IsLockActive);
            Assert.Equal("admin-1", item.ActionByUserId);

            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(1, task.ApprovedItems);
            Assert.Equal(1, task.PendingItems);
            Assert.Equal(QuestionReviewTaskStatus.Assigned, task.Status);
            Assert.Equal(1, await db.QuestionAuditLogs.CountAsync(a => a.QuestionId == qids[0] && a.Action == "مزامنة مع مهمة مراجعة"));
        }

        [Fact]
        public async Task Sync_AdminApprovedAllPending_CompletesTask()
        {
            var h = Create();
            var (_, _, qids, _) = await SeedTaskAsync(h, 2);
            foreach (var id in qids)
                await SetQuestionAsync(h, id, q => { q.IsReviewed = true; q.ReviewedByUserId = Admin.UserId; });

            var affected = await h.Service.SyncAdminDecisionAsync(AdminQuestionDecision.Approved, Admin);

            Assert.Equal(2, affected);
            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(QuestionReviewTaskStatus.Completed, task.Status);
            Assert.Equal(2, task.ApprovedItems);
            Assert.Equal(0, task.PendingItems);
        }

        [Fact]
        public async Task Sync_AdminApprovedReturnedQuestion_ResolvesReturnedItem()
        {
            var h = Create();
            var (_, ids, qids, ins) = await SeedTaskAsync(h, 2);
            await h.Service.ReturnItemAsync(ins.Id, ids[0], "الإجابة الصحيحة غير صحيحة.", Teacher);
            await SetQuestionAsync(h, qids[0], q => { q.IsReviewed = true; q.ReviewedByUserId = Admin.UserId; });

            await h.Service.SyncAdminDecisionAsync(AdminQuestionDecision.Approved, Admin);

            using var db = h.Factory.CreateDbContext();
            var item = await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == ids[0]);
            Assert.Equal(QuestionReviewTaskItemStatus.ApprovedByAdmin, item.Status);
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(0, task.ReturnedItems);
            Assert.Equal(1, task.ApprovedItems);
        }

        [Fact]
        public async Task Sync_AdminRejected_PendingBecomesRemoved_ReturnedBecomesReturnResolved()
        {
            var h = Create();
            var (_, ids, qids, ins) = await SeedTaskAsync(h, 2);
            await h.Service.ReturnItemAsync(ins.Id, ids[1], "السؤال مكرر لسؤال آخر في البنك.", Teacher);
            foreach (var id in qids)
                await SetQuestionAsync(h, id, q => q.IsRejected = true);

            var affected = await h.Service.SyncAdminDecisionAsync(AdminQuestionDecision.Rejected, Admin);

            Assert.Equal(2, affected);
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(QuestionReviewTaskItemStatus.Removed, (await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == ids[0])).Status);
            Assert.Equal(QuestionReviewTaskItemStatus.ReturnResolved, (await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == ids[1])).Status);
            Assert.False(await db.QuestionReviewTaskItems.AnyAsync(i => i.IsLockActive));

            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(1, task.RemovedItems);
            Assert.Equal(0, task.PendingItems);
            Assert.Equal(QuestionReviewTaskStatus.Completed, task.Status);
        }

        [Fact]
        public async Task Sync_AdminUnapproved_ApprovedItemIsRemovedFromTaskAccounting()
        {
            var h = Create();
            var (taskId, ids, qids, ins) = await SeedTaskAsync(h, 2);
            await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { ids[0] }, Teacher);
            await SetQuestionAsync(h, qids[0], q => { q.IsReviewed = false; q.ReviewedByUserId = null; q.ReviewedAt = null; });

            var affected = await h.Service.SyncAdminDecisionAsync(AdminQuestionDecision.Unapproved, Admin);

            Assert.Equal(1, affected);
            using var db = h.Factory.CreateDbContext();
            var item = await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == ids[0]);
            Assert.Equal(QuestionReviewTaskItemStatus.Removed, item.Status);
            Assert.False(item.IsLockActive);

            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(0, task.ApprovedItems);
            Assert.Equal(1, task.RemovedItems);
            Assert.Equal(1, task.PendingItems);

            // السؤال عاد حرًّا في المسار العام
            Assert.Null(await h.Locks.GetHoldAsync(qids[0]));
        }

        [Fact]
        public async Task Sync_Unapproved_IgnoresClosedTasks()
        {
            var h = Create();
            var (taskId, ids, qids, ins) = await SeedTaskAsync(h, 1);
            await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { ids[0] }, Teacher);
            using (var db = h.Factory.CreateDbContext())
            {
                var t = await db.QuestionReviewTasks.SingleAsync();
                t.Status = QuestionReviewTaskStatus.Closed;
                await db.SaveChangesAsync();
            }
            await SetQuestionAsync(h, qids[0], q => q.IsReviewed = false);

            var affected = await h.Service.SyncAdminDecisionAsync(AdminQuestionDecision.Unapproved, Admin);

            Assert.Equal(0, affected);
            using var check = h.Factory.CreateDbContext();
            Assert.Equal(QuestionReviewTaskItemStatus.Approved, (await check.QuestionReviewTaskItems.SingleAsync()).Status);
        }

        [Fact]
        public async Task Sync_NothingToSync_ReturnsZeroAndChangesNothing()
        {
            var h = Create();
            await SeedTaskAsync(h, 2);

            Assert.Equal(0, await h.Service.SyncAdminDecisionAsync(AdminQuestionDecision.Approved, Admin));
            Assert.Equal(0, await h.Service.SyncAdminDecisionAsync(AdminQuestionDecision.Rejected, Admin));
            Assert.Equal(0, await h.Service.SyncAdminDecisionAsync(AdminQuestionDecision.Unapproved, Admin));

            using var db = h.Factory.CreateDbContext();
            Assert.Equal(2, await db.QuestionReviewTaskItems.CountAsync(i => i.Status == QuestionReviewTaskItemStatus.Pending && i.IsLockActive));
        }

        [Fact]
        public async Task Approve_QuestionAlreadyApprovedByAdmin_UpdatesItemInsteadOfSkipping()
        {
            var h = Create();
            var (taskId, ids, qids, ins) = await SeedTaskAsync(h, 2);
            await SetQuestionAsync(h, qids[0], q => { q.IsReviewed = true; q.ReviewedByUserId = Admin.UserId; });

            var result = await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { ids[0], ids[1] }, Teacher);

            Assert.True(result.Success, result.Message);
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(QuestionReviewTaskItemStatus.ApprovedByAdmin, (await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == ids[0])).Status);
            Assert.Equal(QuestionReviewTaskItemStatus.Approved, (await db.QuestionReviewTaskItems.SingleAsync(i => i.Id == ids[1])).Status);
            Assert.False(await db.QuestionReviewTaskItems.AnyAsync(i => i.IsLockActive));
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(2, task.ApprovedItems);
            Assert.Equal(QuestionReviewTaskStatus.Completed, task.Status);
        }

        [Fact]
        public async Task Approve_OnlyAdminApprovedQuestions_SucceedsAndCompletesTask()
        {
            var h = Create();
            var (taskId, ids, qids, ins) = await SeedTaskAsync(h, 1);
            await SetQuestionAsync(h, qids[0], q => { q.IsReviewed = true; q.ReviewedByUserId = Admin.UserId; });

            var result = await h.Service.ApproveItemsAsync(ins.Id, taskId, new[] { ids[0] }, Teacher);

            Assert.True(result.Success, result.Message);
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(QuestionReviewTaskStatus.Completed, (await db.QuestionReviewTasks.SingleAsync()).Status);
        }
    }
}
