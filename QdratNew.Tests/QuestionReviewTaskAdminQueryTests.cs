using Microsoft.EntityFrameworkCore;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.QuestionReviewTasks;
using QdratNew.ViewModels.QuestionReviewTasks;
using Xunit;

namespace QdratNew.Tests
{
    // QRT-S5 — متابعة الأدمن: المؤشرات، الفلاتر والترقيم، التفاصيل والخط الزمني، بطاقة الداشبورد
    public class QuestionReviewTaskAdminQueryTests
    {
        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;
            public FixedTimeProvider(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static readonly DateTime Now = new(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc);

        private static (QuestionReviewTaskAdminQueryService Svc, TestDbContextFactory Factory) Create()
        {
            var factory = new TestDbContextFactory(Guid.NewGuid().ToString());
            return (new QuestionReviewTaskAdminQueryService(factory, new FixedTimeProvider(new DateTimeOffset(Now))), factory);
        }

        private static async Task<Instructor> SeedInstructorAsync(TestDbContextFactory factory, string name)
        {
            using var db = factory.CreateDbContext();
            var i = new Instructor
            {
                FullName = name,
                NationalID = Guid.NewGuid().ToString("N")[..10],
                Email = $"{Guid.NewGuid():N}@test.local",
                Specialization = "عام",
                IsActive = true
            };
            db.Instructors.Add(i);
            await db.SaveChangesAsync();
            return i;
        }

        private static async Task<QuestionReviewTask> SeedTaskAsync(
            TestDbContextFactory factory, Instructor instructor, string code,
            QuestionReviewTaskStatus status = QuestionReviewTaskStatus.Assigned,
            int total = 10, int pending = 10, int approved = 0, int returned = 0, int removed = 0,
            DateTime? due = null, DateTime? created = null,
            QuestionReviewTaskPriority priority = QuestionReviewTaskPriority.Normal, string? title = null)
        {
            using var db = factory.CreateDbContext();
            var t = new QuestionReviewTask
            {
                Code = code,
                Title = title ?? $"مهمة {code}",
                InstructorId = instructor.Id,
                Status = status,
                Priority = priority,
                DueAtUtc = due,
                CreatedByUserId = "admin-1",
                CreatedByName = "مدير النظام",
                CreatedAtUtc = created ?? Now.AddDays(-1),
                TotalItems = total,
                PendingItems = pending,
                ApprovedItems = approved,
                ReturnedItems = returned,
                RemovedItems = removed
            };
            db.QuestionReviewTasks.Add(t);
            await db.SaveChangesAsync();
            return t;
        }

        private static async Task<Question> SeedQuestionAsync(TestDbContextFactory factory, string title)
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

            var q = new Question
            {
                Title = title,
                ReferenceNumber = $"Q-{Guid.NewGuid():N}"[..12],
                CurriculumId = 1,
                LessonId = 1,
                SectionId = 1,
                CorrectAnswer = "أ",
                IsComplete = true,
                Options = new List<QuestionOption> { new() { Text = "أ" }, new() { Text = "ب" } }
            };
            db.Questions.Add(q);
            await db.SaveChangesAsync();
            return q;
        }

        private static async Task SeedItemAsync(
            TestDbContextFactory factory, QuestionReviewTask task, Question q, int order,
            QuestionReviewTaskItemStatus status, DateTime? actionAt = null, string? by = null, string? returnNote = null)
        {
            using var db = factory.CreateDbContext();
            db.QuestionReviewTaskItems.Add(new QuestionReviewTaskItem
            {
                TaskId = task.Id,
                QuestionId = q.Id,
                SortOrder = order,
                Status = status,
                IsLockActive = status == QuestionReviewTaskItemStatus.Pending,
                ActionAtUtc = actionAt,
                ActionByName = by,
                ReturnNote = returnNote,
                ReferenceNumberSnapshot = q.ReferenceNumber
            });
            await db.SaveChangesAsync();
        }

        private static readonly AdminTasksFilter NoFilter = new();

        // ---------------------------------------------------------------- KPIs

        [Fact]
        public async Task Index_Kpis_UseStoredCounters_AndExcludeCancelled()
        {
            var (svc, f) = Create();
            var ins = await SeedInstructorAsync(f, "أ. محمد");

            // مفتوحة ومتأخرة: 10 أسئلة، 4 معتمدة، 5 معلقة، 1 مرتجع
            await SeedTaskAsync(f, ins, "QRT-2026-0001", QuestionReviewTaskStatus.InProgress,
                total: 10, pending: 5, approved: 4, returned: 1, due: Now.AddDays(-1));
            // مفتوحة غير متأخرة
            await SeedTaskAsync(f, ins, "QRT-2026-0002", QuestionReviewTaskStatus.Assigned,
                total: 10, pending: 10, due: Now.AddDays(3));
            // مكتملة: لا تُحسب مفتوحة لكن تدخل في نسبة الاعتماد
            await SeedTaskAsync(f, ins, "QRT-2026-0003", QuestionReviewTaskStatus.Completed,
                total: 10, pending: 0, approved: 10, due: Now.AddDays(-5));
            // ملغاة: خارج كل المؤشرات
            await SeedTaskAsync(f, ins, "QRT-2026-0004", QuestionReviewTaskStatus.Cancelled,
                total: 50, pending: 50, returned: 9, due: Now.AddDays(-9));

            var vm = await svc.GetIndexAsync();

            Assert.Equal(2, vm.Kpis.OpenTasks);
            Assert.Equal(15, vm.Kpis.LockedPending);          // 5 + 10 (المفتوحة فقط)
            Assert.Equal(1, vm.Kpis.OverdueTasks);
            Assert.Equal(1, vm.Kpis.UnresolvedReturns);
            Assert.Equal(47, vm.Kpis.AverageApprovalPercent); // (4+0+10) / 30 = 46.7% → 47
        }

        [Fact]
        public async Task Index_WithNoTasks_ReturnsZeros()
        {
            var (svc, _) = Create();

            var vm = await svc.GetIndexAsync();

            Assert.Equal(0, vm.Kpis.OpenTasks);
            Assert.Equal(0, vm.Kpis.AverageApprovalPercent);
            Assert.Empty(vm.ByInstructor);
            Assert.Empty(vm.Instructors);
        }

        [Fact]
        public async Task Index_ByInstructor_GroupsAndComputesPercent()
        {
            var (svc, f) = Create();
            var a = await SeedInstructorAsync(f, "أ. محمد");
            var b = await SeedInstructorAsync(f, "أ. خالد");

            await SeedTaskAsync(f, a, "QRT-2026-0001", total: 10, pending: 5, approved: 5);
            await SeedTaskAsync(f, a, "QRT-2026-0002", total: 10, pending: 0, approved: 10, status: QuestionReviewTaskStatus.Completed);
            await SeedTaskAsync(f, b, "QRT-2026-0003", total: 8, pending: 8, approved: 0);

            var vm = await svc.GetIndexAsync();

            Assert.Equal(2, vm.Instructors.Count);
            var rowA = Assert.Single(vm.ByInstructor, x => x.InstructorId == a.Id);
            Assert.Equal(2, rowA.Tasks);
            Assert.Equal(75, rowA.ApprovalPercent);   // 15 / 20
            Assert.Equal(5, rowA.Pending);
            // الأعلى معلقًا أولًا
            Assert.Equal(b.Id, vm.ByInstructor[0].InstructorId);
        }

        // ------------------------------------------------------------ Tasks page

        [Fact]
        public async Task TasksPage_Filters_ByStatusInstructorOverdueSearchAndPriority()
        {
            var (svc, f) = Create();
            var a = await SeedInstructorAsync(f, "أ. محمد");
            var b = await SeedInstructorAsync(f, "أ. خالد");

            await SeedTaskAsync(f, a, "QRT-2026-0001", QuestionReviewTaskStatus.InProgress, due: Now.AddDays(-2), title: "الجبر");
            await SeedTaskAsync(f, a, "QRT-2026-0002", QuestionReviewTaskStatus.Completed, title: "الهندسة");
            await SeedTaskAsync(f, b, "QRT-2026-0003", QuestionReviewTaskStatus.Assigned, due: Now.AddDays(2), title: "الجبر المتقدم",
                priority: QuestionReviewTaskPriority.Urgent);

            var byStatus = await svc.GetTasksPageAsync(new AdminTasksFilter { Status = QuestionReviewTaskStatus.Completed }, 0, 25, -1, true);
            Assert.Equal("QRT-2026-0002", Assert.Single(byStatus.Rows).Code);
            Assert.Equal(3, byStatus.Total);
            Assert.Equal(1, byStatus.Filtered);

            var byInstructor = await svc.GetTasksPageAsync(new AdminTasksFilter { InstructorId = b.Id }, 0, 25, -1, true);
            Assert.Equal("QRT-2026-0003", Assert.Single(byInstructor.Rows).Code);

            var overdue = await svc.GetTasksPageAsync(new AdminTasksFilter { OverdueOnly = true }, 0, 25, -1, true);
            var overdueRow = Assert.Single(overdue.Rows);
            Assert.Equal("QRT-2026-0001", overdueRow.Code);
            Assert.True(overdueRow.IsOverdue);

            var search = await svc.GetTasksPageAsync(new AdminTasksFilter { Search = "الجبر" }, 0, 25, -1, true);
            Assert.Equal(2, search.Rows.Count);

            var urgent = await svc.GetTasksPageAsync(new AdminTasksFilter { Priority = QuestionReviewTaskPriority.Urgent }, 0, 25, -1, true);
            Assert.Equal("QRT-2026-0003", Assert.Single(urgent.Rows).Code);
        }

        [Fact]
        public async Task TasksPage_DefaultOrder_NewestFirst_AndRowsCarryPercents()
        {
            var (svc, f) = Create();
            var a = await SeedInstructorAsync(f, "أ. محمد");
            await SeedTaskAsync(f, a, "QRT-2026-0001");
            await SeedTaskAsync(f, a, "QRT-2026-0002", total: 20, pending: 10, approved: 5, returned: 5);

            var page = await svc.GetTasksPageAsync(NoFilter, 0, 25, -1, true);

            Assert.Equal(new[] { "QRT-2026-0002", "QRT-2026-0001" }, page.Rows.Select(r => r.Code).ToArray());
            var top = page.Rows[0];
            Assert.Equal(25, top.ApprovalPercent);   // 5 / 20
            Assert.Equal(50, top.HandledPercent);    // (20-10) / 20
            Assert.Equal("أ. محمد", top.InstructorName);
        }

        [Fact]
        public async Task TasksPage_PagingIsServerSide_AndHugeLengthIsCapped()
        {
            var (svc, f) = Create();
            var a = await SeedInstructorAsync(f, "أ. محمد");
            for (var i = 1; i <= 7; i++)
                await SeedTaskAsync(f, a, $"QRT-2026-{i:0000}");

            var second = await svc.GetTasksPageAsync(NoFilter, 3, 3, -1, true);
            Assert.Equal(3, second.Rows.Count);
            Assert.Equal(7, second.Filtered);

            var capped = await svc.GetTasksPageAsync(NoFilter, 0, 5000, -1, true);
            Assert.Equal(7, capped.Rows.Count);
        }

        [Fact]
        public async Task TasksPage_OrderByWhitelistedColumn_Works_AndUnknownColumnFallsBack()
        {
            var (svc, f) = Create();
            var a = await SeedInstructorAsync(f, "أ. محمد");
            await SeedTaskAsync(f, a, "QRT-2026-0001", total: 5, pending: 5);
            await SeedTaskAsync(f, a, "QRT-2026-0002", total: 30, pending: 30);

            var asc = await svc.GetTasksPageAsync(NoFilter, 0, 25, 3, false);
            Assert.Equal("QRT-2026-0001", asc.Rows[0].Code);

            var unknown = await svc.GetTasksPageAsync(NoFilter, 0, 25, 99, false);
            Assert.Equal("QRT-2026-0002", unknown.Rows[0].Code);   // الافتراضي الأحدث أولًا
        }

        // --------------------------------------------------------------- Details

        [Fact]
        public async Task Details_UnknownTask_ReturnsNull()
        {
            var (svc, _) = Create();
            Assert.Null(await svc.GetDetailsAsync(999));
        }

        [Fact]
        public async Task Details_BuildsTimeline_NewestFirst_WithItemEvents()
        {
            var (svc, f) = Create();
            var ins = await SeedInstructorAsync(f, "أ. محمد");
            var task = await SeedTaskAsync(f, ins, "QRT-2026-0001", QuestionReviewTaskStatus.InProgress,
                total: 2, pending: 0, approved: 1, returned: 1, created: Now.AddDays(-3));

            using (var db = f.CreateDbContext())
            {
                var t = await db.QuestionReviewTasks.FirstAsync(x => x.Id == task.Id);
                t.StartedAtUtc = Now.AddDays(-2);
                await db.SaveChangesAsync();
            }

            var q1 = await SeedQuestionAsync(f, "س1");
            var q2 = await SeedQuestionAsync(f, "س2");
            await SeedItemAsync(f, task, q1, 1, QuestionReviewTaskItemStatus.Approved, Now.AddDays(-1), "أ. محمد");
            await SeedItemAsync(f, task, q2, 2, QuestionReviewTaskItemStatus.Returned, Now.AddHours(-5), "أ. محمد", "الإجابة غير دقيقة تمامًا");

            var vm = await svc.GetDetailsAsync(task.Id);

            Assert.NotNull(vm);
            Assert.Equal("أ. محمد", vm!.InstructorName);
            Assert.Equal(4, vm.Timeline.Count);   // إنشاء + بدء + اعتماد + إرجاع
            Assert.Equal(vm.Timeline.OrderByDescending(e => e.AtUtc).Select(e => e.AtUtc), vm.Timeline.Select(e => e.AtUtc));
            Assert.Contains(vm.Timeline, e => e.Text.StartsWith("إرجاع") && e.Detail == "الإجابة غير دقيقة تمامًا");
            Assert.Equal(50, vm.Progress.ApprovalPercent);
        }

        [Fact]
        public async Task Details_Cancelled_ShowsReasonInTimeline()
        {
            var (svc, f) = Create();
            var ins = await SeedInstructorAsync(f, "أ. محمد");
            var task = await SeedTaskAsync(f, ins, "QRT-2026-0001", QuestionReviewTaskStatus.Cancelled);
            using (var db = f.CreateDbContext())
            {
                var t = await db.QuestionReviewTasks.FirstAsync(x => x.Id == task.Id);
                t.CancelledAtUtc = Now;
                t.CancelReason = "أُسندت بالخطأ";
                await db.SaveChangesAsync();
            }

            var vm = await svc.GetDetailsAsync(task.Id);

            Assert.Contains(vm!.Timeline, e => e.Text == "أُلغيت المهمة" && e.Detail == "أُسندت بالخطأ");
        }

        // ----------------------------------------------------------------- Items

        [Fact]
        public async Task ItemsPage_UnknownTask_ReturnsNull()
        {
            var (svc, _) = Create();
            Assert.Null(await svc.GetItemsPageAsync(999, 0, 25, null, null));
        }

        [Fact]
        public async Task ItemsPage_FiltersByStatus_AndExposesReturnNote()
        {
            var (svc, f) = Create();
            var ins = await SeedInstructorAsync(f, "أ. محمد");
            var task = await SeedTaskAsync(f, ins, "QRT-2026-0001", total: 3);
            var qs = new[] { await SeedQuestionAsync(f, "أ"), await SeedQuestionAsync(f, "ب"), await SeedQuestionAsync(f, "ج") };
            await SeedItemAsync(f, task, qs[0], 1, QuestionReviewTaskItemStatus.Pending);
            await SeedItemAsync(f, task, qs[1], 2, QuestionReviewTaskItemStatus.Returned, Now, "أ. محمد", "ملاحظة الإرجاع الأولى");
            await SeedItemAsync(f, task, qs[2], 3, QuestionReviewTaskItemStatus.Approved, Now, "أ. محمد");

            var all = await svc.GetItemsPageAsync(task.Id, 0, 25, null, null);
            Assert.Equal(3, all!.Total);
            Assert.Equal(3, all.Filtered);

            var returned = await svc.GetItemsPageAsync(task.Id, 0, 25, (int)QuestionReviewTaskItemStatus.Returned, null);
            var row = Assert.Single(returned!.Rows);
            Assert.Equal("ملاحظة الإرجاع الأولى", row.ReturnNote);
            Assert.Equal(1, returned.Filtered);
            Assert.Equal(3, returned.Total);
        }

        // ------------------------------------------------------------- Dashboard

        [Fact]
        public async Task DashboardSummary_UsesOpenTasks_AndNinetyDayWindowForPercent()
        {
            var (svc, f) = Create();
            var ins = await SeedInstructorAsync(f, "أ. محمد");

            // مفتوحة حديثة: 10 أسئلة، 5 معتمدة، 5 معلقة، متأخرة
            await SeedTaskAsync(f, ins, "QRT-2026-0001", QuestionReviewTaskStatus.InProgress,
                total: 10, pending: 5, approved: 5, due: Now.AddDays(-1), created: Now.AddDays(-10));
            // مكتملة داخل 90 يومًا: 10 معتمدة
            await SeedTaskAsync(f, ins, "QRT-2026-0002", QuestionReviewTaskStatus.Completed,
                total: 10, pending: 0, approved: 10, created: Now.AddDays(-30));
            // مكتملة أقدم من 90 يومًا: خارج النسبة
            await SeedTaskAsync(f, ins, "QRT-2026-0003", QuestionReviewTaskStatus.Completed,
                total: 100, pending: 0, approved: 0, created: Now.AddDays(-200));
            // ملغاة: مستبعدة
            await SeedTaskAsync(f, ins, "QRT-2026-0004", QuestionReviewTaskStatus.Cancelled,
                total: 100, pending: 100, created: Now.AddDays(-2));

            var s = await svc.GetDashboardSummaryAsync();

            Assert.Equal(1, s.OpenTasks);
            Assert.Equal(5, s.LockedPending);
            Assert.Equal(1, s.Overdue);
            Assert.Equal(75, s.ApprovalPercent);   // (5 + 10) / 20
        }

        [Fact]
        public async Task DashboardSummary_NoTasks_ReturnsZeros()
        {
            var (svc, _) = Create();
            var s = await svc.GetDashboardSummaryAsync();
            Assert.Equal(0, s.OpenTasks);
            Assert.Equal(0, s.ApprovalPercent);
        }
    }
}
