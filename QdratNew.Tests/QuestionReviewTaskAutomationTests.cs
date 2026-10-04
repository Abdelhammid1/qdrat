using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Jobs;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.Services.Interfaces;
using QdratNew.Services.QuestionReviewTasks;
using QdratNew.ViewModels.QuestionReviewTasks;
using Xunit;

namespace QdratNew.Tests
{
    // QRT-S7 — التوزيع التلقائي، Job التذكير + الملخص اليومي، تقرير أداء المراجعين
    public class QuestionReviewTaskAutomationTests
    {
        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;
            public FixedTimeProvider(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static readonly DateTime Now = new(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc);
        private static readonly ReviewActor Admin = new("admin-1", "مدير النظام", UserRoleType.Admin);

        private sealed class Harness
        {
            public required QuestionReviewTaskService Service { get; init; }
            public required TestDbContextFactory Factory { get; init; }
            public required Mock<IAdvancedNotificationService> Notifications { get; init; }
            public required FixedTimeProvider Time { get; init; }
        }

        private static Harness Create()
        {
            var factory = new TestDbContextFactory(Guid.NewGuid().ToString());
            var scope = new Mock<IInstructorScopeService>();
            scope.Setup(s => s.GetDirectCurriculumIdsAsync(It.IsAny<int>())).ReturnsAsync(new List<int> { 1 });
            var notifications = new Mock<IAdvancedNotificationService>();
            var time = new FixedTimeProvider(new DateTimeOffset(Now));

            return new Harness
            {
                Service = new QuestionReviewTaskService(factory, time, scope.Object, notifications.Object,
                    NullLogger<QuestionReviewTaskService>.Instance),
                Factory = factory,
                Notifications = notifications,
                Time = time
            };
        }

        private static QuestionReviewTaskReminderJob CreateJob(Harness h)
            => new(h.Factory, h.Time, h.Notifications.Object, NullLogger<QuestionReviewTaskReminderJob>.Instance);

        private static QuestionReviewTaskAdminQueryService CreateReport(Harness h)
            => new(h.Factory, h.Time);

        // مدرب نشط بحساب دخول، مرتبط بالمنهج 1 (يُنشأ الربط عبر دفعة نشطة) ما لم يُطلب غير ذلك
        private static async Task<Instructor> SeedInstructorAsync(
            TestDbContextFactory factory, string userId, string name, bool linkCurriculum = true, int? partnerId = null)
        {
            using var db = factory.CreateDbContext();
            if (!await db.Batches.AnyAsync(b => b.Id == 1))
                db.Batches.Add(new Batch { Id = 1, Name = "دفعة اختبار", IsActive = true, IsDeleted = false, IsArchived = false });

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

            if (linkCurriculum)
            {
                db.InstructorCurriculumBatches.Add(new InstructorCurriculumBatch { InstructorId = i.Id, CurriculumId = 1, BatchId = 1, UserId = userId });
                await db.SaveChangesAsync();
            }
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

            var list = Enumerable.Range(0, count).Select(i => new Question
            {
                Title = $"سؤال {Guid.NewGuid():N}"[..14],
                ReferenceNumber = $"Q-{Guid.NewGuid():N}"[..12],
                CurriculumId = 1,
                LessonId = 1,
                SectionId = 1,
                CorrectAnswer = "أ",
                IsComplete = true,
                CreatedAt = new DateTime(2026, 1, 1).AddDays(i),
                Options = new List<QuestionOption> { new() { Text = "أ" }, new() { Text = "ب" } }
            }).ToList();
            db.Questions.AddRange(list);
            await db.SaveChangesAsync();
            return list;
        }

        private static async Task<int> CreateTaskAsync(Harness h, Instructor instructor, int count, DateTime? dueUtc = null)
        {
            var questions = await SeedQuestionsAsync(h.Factory, count);
            var r = await h.Service.CreateTaskAsync(new CreateQuestionReviewTaskInput
            {
                Title = "مراجعة",
                InstructorId = instructor.Id,
                SelectedQuestionIds = questions.Select(q => q.Id).ToList()
            }, Admin);
            Assert.True(r.Success, r.Message);

            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.OrderByDescending(t => t.Id).FirstAsync();
            if (dueUtc.HasValue)
            {
                task.DueAtUtc = dueUtc;
                await db.SaveChangesAsync();
            }
            return task.Id;
        }

        private static AutoDistributeInput DistInput(IEnumerable<int> instructorIds, int take, string title = "توزيع الجبر")
            => new() { Title = title, TakeCount = take, InstructorIds = instructorIds.ToList() };

        // ───── خوارزمية التوزيع ─────

        [Fact]
        public void Allocate_SplitsEvenly_AndRemainderGoesToFirst()
        {
            Assert.Equal(new[] { 4, 3, 3 }, QuestionReviewTaskDistributor.Allocate(10, new[] { 0, 0, 0 }));
            Assert.Equal(new[] { 2, 2 }, QuestionReviewTaskDistributor.Allocate(4, new[] { 0, 0 }));
        }

        [Fact]
        public void Allocate_RespectsCurrentLoad_LeastLoadedTakesMore()
        {
            // العبء 0 و4: أول 4 أسئلة كلها للأقل عبئًا ثم يتعادلان
            Assert.Equal(new[] { 4, 0 }, QuestionReviewTaskDistributor.Allocate(4, new[] { 0, 4 }));
            Assert.Equal(new[] { 5, 1 }, QuestionReviewTaskDistributor.Allocate(6, new[] { 0, 4 }));
        }

        [Fact]
        public void Allocate_ZeroTotalOrNoInstructors_ReturnsZeros()
        {
            Assert.Equal(new[] { 0, 0 }, QuestionReviewTaskDistributor.Allocate(0, new[] { 0, 0 }));
            Assert.Empty(QuestionReviewTaskDistributor.Allocate(5, Array.Empty<int>()));
        }

        // ───── 7.2 التوزيع التلقائي ─────

        [Fact]
        public async Task AutoDistribute_CreatesIndependentTask_PerInstructor_WithAllQuestionsLocked()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            var b = await SeedInstructorAsync(h.Factory, "u-b", "أ. بدر");
            var c = await SeedInstructorAsync(h.Factory, "u-c", "أ. جمال");
            await SeedQuestionsAsync(h.Factory, 10);

            var r = await h.Service.AutoDistributeAsync(DistInput(new[] { a.Id, b.Id, c.Id }, 10), Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var tasks = await db.QuestionReviewTasks.OrderBy(t => t.Code).ToListAsync();
            Assert.Equal(3, tasks.Count);
            Assert.Equal(10, tasks.Sum(t => t.TotalItems));
            Assert.True(tasks.Max(t => t.TotalItems) - tasks.Min(t => t.TotalItems) <= 1);
            Assert.All(tasks, t =>
            {
                Assert.Equal(t.TotalItems, t.PendingItems);
                Assert.Equal(QuestionReviewTaskStatus.Assigned, t.Status);
                Assert.Contains("—", t.Title);
            });
            Assert.Equal(3, tasks.Select(t => t.Code).Distinct().Count());   // أكواد متتابعة فريدة

            var items = await db.QuestionReviewTaskItems.ToListAsync();
            Assert.Equal(10, items.Count);
            Assert.All(items, i => Assert.True(i.IsLockActive));
            Assert.Equal(10, items.Select(i => i.QuestionId).Distinct().Count());   // لا سؤال مكرر بين المهام

            Assert.Equal(10, await db.QuestionAuditLogs.CountAsync(l => l.ChangedFieldsSummary!.Contains("توزيع تلقائي")));
            h.Notifications.Verify(n => n.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>(),
                NotificationCategory.Important, It.Is<string>(u => u.StartsWith("/Instructors/QuestionReviewTasks/Review/"))), Times.Exactly(3));
        }

        [Fact]
        public async Task AutoDistribute_ConsidersCurrentLoad_AndSkipsInstructorWithZeroShare()
        {
            var h = Create();
            var busy = await SeedInstructorAsync(h.Factory, "u-busy", "أ. مشغول");
            var free = await SeedInstructorAsync(h.Factory, "u-free", "أ. متفرغ");
            await CreateTaskAsync(h, busy, 4);   // عبء 4 محجوز
            await SeedQuestionsAsync(h.Factory, 3);

            var r = await h.Service.AutoDistributeAsync(DistInput(new[] { busy.Id, free.Id }, 3), Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var created = await db.QuestionReviewTasks.Where(t => t.AdminNote == null && t.Title.StartsWith("توزيع الجبر")).ToListAsync();
            Assert.Single(created);                       // المشغول لم يأخذ شيئًا فلا مهمة فارغة له
            Assert.Equal(free.Id, created[0].InstructorId);
            Assert.Equal(3, created[0].TotalItems);
        }

        [Fact]
        public async Task AutoDistribute_ExcludesIneligibleInstructor_ButContinuesWithOthers()
        {
            var h = Create();
            var ok = await SeedInstructorAsync(h.Factory, "u-ok", "أ. مؤهل");
            var noCurriculum = await SeedInstructorAsync(h.Factory, "u-no", "أ. بلا منهج", linkCurriculum: false);
            var otherPartner = await SeedInstructorAsync(h.Factory, "u-p", "أ. شريك", partnerId: 9);
            await SeedQuestionsAsync(h.Factory, 4);

            var r = await h.Service.AutoDistributeAsync(DistInput(new[] { ok.Id, noCurriculum.Id, otherPartner.Id }, 4), Admin);

            Assert.True(r.Success, r.Message);
            Assert.Contains("استُبعد 2", r.Message);
            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(ok.Id, task.InstructorId);
            Assert.Equal(4, task.TotalItems);
        }

        [Fact]
        public async Task AutoDistribute_NoEligibleInstructor_FailsWithoutAnyInsert()
        {
            var h = Create();
            var noCurriculum = await SeedInstructorAsync(h.Factory, "u-no", "أ. بلا منهج", linkCurriculum: false);
            await SeedQuestionsAsync(h.Factory, 3);

            var r = await h.Service.AutoDistributeAsync(DistInput(new[] { noCurriculum.Id }, 3), Admin);

            Assert.False(r.Success);
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(0, await db.QuestionReviewTasks.CountAsync());
            Assert.Equal(0, await db.QuestionReviewTaskItems.CountAsync());
            Assert.Equal(0, await db.QuestionAuditLogs.CountAsync());
            h.Notifications.Verify(n => n.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationCategory>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task AutoDistribute_SkipsAlreadyLockedQuestions()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            var b = await SeedInstructorAsync(h.Factory, "u-b", "أ. بدر");
            await CreateTaskAsync(h, a, 3);          // 3 أسئلة محجوزة
            await SeedQuestionsAsync(h.Factory, 2);  // 2 حرّة فقط

            var r = await h.Service.AutoDistributeAsync(DistInput(new[] { a.Id, b.Id }, 10), Admin);

            Assert.True(r.Success, r.Message);
            using var db = h.Factory.CreateDbContext();
            var newItems = await db.QuestionReviewTaskItems.CountAsync(i => i.Task!.Title.StartsWith("توزيع الجبر"));
            Assert.Equal(2, newItems);
            Assert.Equal(5, await db.QuestionReviewTaskItems.Where(i => i.IsLockActive).Select(i => i.QuestionId).Distinct().CountAsync());
        }

        [Fact]
        public async Task AutoDistribute_RejectsInvalidInput()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            await SeedQuestionsAsync(h.Factory, 3);

            Assert.False((await h.Service.AutoDistributeAsync(DistInput(new[] { a.Id }, 3, "ab"), Admin)).Success);          // عنوان قصير
            Assert.False((await h.Service.AutoDistributeAsync(DistInput(new[] { a.Id }, 0), Admin)).Success);                // عدد صفر
            Assert.False((await h.Service.AutoDistributeAsync(DistInput(new[] { a.Id }, 501), Admin)).Success);              // فوق الحد
            Assert.False((await h.Service.AutoDistributeAsync(DistInput(Array.Empty<int>(), 3), Admin)).Success);            // بلا مدربين
            Assert.False((await h.Service.AutoDistributeAsync(DistInput(Enumerable.Range(1, 21), 3), Admin)).Success);       // >20 مدربًا

            var past = DistInput(new[] { a.Id }, 3);
            past.DueAtLocal = new DateTime(2020, 1, 1, 10, 0, 0);
            Assert.False((await h.Service.AutoDistributeAsync(past, Admin)).Success);                                         // موعد ماضٍ

            using var db = h.Factory.CreateDbContext();
            Assert.Equal(0, await db.QuestionReviewTasks.CountAsync());
        }

        [Fact]
        public async Task PreviewAutoDistribution_ReturnsPlan_WithoutWriting()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            var b = await SeedInstructorAsync(h.Factory, "u-b", "أ. بدر");
            await SeedQuestionsAsync(h.Factory, 5);

            var r = await h.Service.PreviewAutoDistributionAsync(DistInput(new[] { a.Id, b.Id }, 5));

            Assert.True(r.Success, r.Message);
            var preview = Assert.IsType<AutoDistributionPreviewDto>(r.Data);
            Assert.Equal(5, preview.QuestionCount);
            Assert.Equal(2, preview.Lines.Count);
            Assert.Equal(5, preview.Lines.Sum(l => l.Assigned));
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(0, await db.QuestionReviewTasks.CountAsync());
            Assert.Equal(0, await db.QuestionAuditLogs.CountAsync());
        }

        // ───── 7.1 + 7.4 الـ Job ─────

        [Fact]
        public async Task Job_RemindsInstructor_WhenDueWithin24h_AndStampsLastReminder()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            var taskId = await CreateTaskAsync(h, a, 2, Now.AddHours(10));

            var result = await CreateJob(h).RunAsync(CancellationToken.None);

            Assert.Equal(1, result.InstructorReminders);
            Assert.Equal(0, result.CreatorAlerts);
            h.Notifications.Verify(n => n.SendToUserAsync("u-a", It.Is<string>(m => m.Contains("موعد تسليمها")),
                NotificationCategory.Reminder, $"/Instructors/QuestionReviewTasks/Review/{taskId}"), Times.Once);

            using var db = h.Factory.CreateDbContext();
            Assert.Equal(Now, (await db.QuestionReviewTasks.SingleAsync()).LastReminderAtUtc);
        }

        [Fact]
        public async Task Job_IgnoresTasks_DueLaterThan24h_OrWithoutDue_OrNotActive()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            await CreateTaskAsync(h, a, 1, Now.AddDays(3));     // بعيد
            await CreateTaskAsync(h, a, 1, null);               // بلا موعد
            var cancelled = await CreateTaskAsync(h, a, 1, Now.AddHours(2));
            using (var db = h.Factory.CreateDbContext())
            {
                (await db.QuestionReviewTasks.SingleAsync(t => t.Id == cancelled)).Status = QuestionReviewTaskStatus.Cancelled;
                await db.SaveChangesAsync();
            }

            var result = await CreateJob(h).RunAsync(CancellationToken.None);

            Assert.Equal(0, result.InstructorReminders);
            h.Notifications.Verify(n => n.SendToUserAsync("u-a", It.IsAny<string>(), NotificationCategory.Reminder, It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Job_SkipsRecentlyReminded_AndDoesNotDuplicateOnSecondRun()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            var recent = await CreateTaskAsync(h, a, 1, Now.AddHours(5));
            using (var db = h.Factory.CreateDbContext())
            {
                (await db.QuestionReviewTasks.SingleAsync(t => t.Id == recent)).LastReminderAtUtc = Now.AddHours(-5);
                await db.SaveChangesAsync();
            }
            var fresh = await CreateTaskAsync(h, a, 1, Now.AddHours(5));

            var job = CreateJob(h);
            var first = await job.RunAsync(CancellationToken.None);
            var second = await job.RunAsync(CancellationToken.None);

            Assert.Equal(1, first.InstructorReminders);    // الحديثة تُتخطّى
            Assert.Equal(0, second.InstructorReminders);   // التشغيل الثاني بنفس اليوم لا يكرر
            h.Notifications.Verify(n => n.SendToUserAsync("u-a", It.IsAny<string>(), NotificationCategory.Reminder,
                $"/Instructors/QuestionReviewTasks/Review/{fresh}"), Times.Once);
        }

        [Fact]
        public async Task Job_Overdue_RemindsInstructor_AndSendsOneAggregatedAlertToCreator()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            await CreateTaskAsync(h, a, 1, Now.AddHours(-30));
            await CreateTaskAsync(h, a, 1, Now.AddHours(-2));

            var result = await CreateJob(h).RunAsync(CancellationToken.None);

            Assert.Equal(2, result.InstructorReminders);
            Assert.Equal(1, result.CreatorAlerts);   // إشعار واحد للمنشئ يجمع المهمتين
            h.Notifications.Verify(n => n.SendToUserAsync("admin-1", It.Is<string>(m => m.Contains("2 مهمة مراجعة متأخرة")),
                NotificationCategory.Important, "/Admin/QuestionReviewTasks"), Times.Once);
            h.Notifications.Verify(n => n.SendToUserAsync("u-a", It.Is<string>(m => m.Contains("متأخرة")),
                NotificationCategory.Reminder, It.IsAny<string?>()), Times.Exactly(2));
        }

        [Fact]
        public async Task Job_DailySummary_ReportsCompletedOverdueAndDueSoonToCreator()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");

            // مهمة اكتملت الآن
            var doneTask = await CreateTaskAsync(h, a, 2);
            List<long> itemIds;
            using (var db = h.Factory.CreateDbContext())
                itemIds = await db.QuestionReviewTaskItems.Where(i => i.TaskId == doneTask).Select(i => i.Id).ToListAsync();
            var approve = await h.Service.ApproveItemsAsync(a.Id, doneTask, itemIds, new ReviewActor("u-a", "أ. أحمد", UserRoleType.Instructor));
            Assert.True(approve.Success, approve.Message);

            await CreateTaskAsync(h, a, 1, Now.AddHours(-3));   // متأخرة
            await CreateTaskAsync(h, a, 1, Now.AddHours(6));    // قريبة

            var result = await CreateJob(h).RunAsync(CancellationToken.None);

            Assert.Equal(1, result.DailySummaries);
            h.Notifications.Verify(n => n.SendToUserAsync("admin-1",
                It.Is<string>(m => m.Contains("ملخص مهام المراجعة اليومي") && m.Contains("اكتملت 1") && m.Contains("متأخرة 1")
                                   && m.Contains("خلال 24 ساعة 1")),
                NotificationCategory.Important, "/Admin/QuestionReviewTasks"), Times.Once);
        }

        [Fact]
        public async Task Job_NothingToReport_SendsNothing()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            await CreateTaskAsync(h, a, 1, Now.AddDays(10));   // نشطة وبعيدة: لا تذكير ولا ملخص
            h.Notifications.Invocations.Clear();

            var result = await CreateJob(h).RunAsync(CancellationToken.None);

            Assert.Equal(new ReminderRunResult(0, 0, 0), result);
            h.Notifications.Verify(n => n.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationCategory>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Job_NotificationFailure_DoesNotThrow_AndStillStampsTasks()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            await CreateTaskAsync(h, a, 1, Now.AddHours(4));
            h.Notifications.Setup(n => n.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationCategory>(), It.IsAny<string?>())).ThrowsAsync(new InvalidOperationException("boom"));

            var result = await CreateJob(h).RunAsync(CancellationToken.None);

            Assert.Equal(new ReminderRunResult(0, 0, 0), result);
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(Now, (await db.QuestionReviewTasks.SingleAsync()).LastReminderAtUtc);
        }

        // ───── 7.3 تقرير أداء المراجعين ─────

        [Fact]
        public async Task Report_AggregatesPerInstructor_ApprovedReturnedRateAndAverageTime()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            var b = await SeedInstructorAsync(h.Factory, "u-b", "أ. بدر");
            var actorA = new ReviewActor("u-a", "أ. أحمد", UserRoleType.Instructor);
            var actorB = new ReviewActor("u-b", "أ. بدر", UserRoleType.Instructor);

            // أحمد: 3 أسئلة (1 معتمد، 1 مرتجع، 1 معلّق)
            var t1 = await CreateTaskAsync(h, a, 3);
            List<long> ids1;
            using (var db = h.Factory.CreateDbContext())
                ids1 = await db.QuestionReviewTaskItems.Where(i => i.TaskId == t1).OrderBy(i => i.SortOrder).Select(i => i.Id).ToListAsync();
            Assert.True((await h.Service.ApproveItemsAsync(a.Id, t1, new[] { ids1[0] }, actorA)).Success);
            Assert.True((await h.Service.ReturnItemAsync(a.Id, ids1[1], "الصياغة غير واضحة وتحتاج مراجعة", actorA)).Success);

            // بدر: مهمة مكتملة
            var t2 = await CreateTaskAsync(h, b, 2);
            List<long> ids2;
            using (var db = h.Factory.CreateDbContext())
                ids2 = await db.QuestionReviewTaskItems.Where(i => i.TaskId == t2).Select(i => i.Id).ToListAsync();
            Assert.True((await h.Service.ApproveItemsAsync(b.Id, t2, ids2, actorB)).Success);

            var report = await CreateReport(h).GetReviewersReportAsync(new ReviewersReportFilter());

            Assert.Equal(2, report.Rows.Count);
            var ra = report.Rows.Single(r => r.InstructorId == a.Id);
            Assert.Equal(1, ra.AssignedTasks);
            Assert.Equal(0, ra.CompletedTasks);
            Assert.Equal(1, ra.ApprovedQuestions);
            Assert.Equal(1, ra.ReturnedQuestions);
            Assert.Equal(2, ra.HandledQuestions);
            Assert.Equal(1, ra.PendingQuestions);
            Assert.Equal(50, ra.ReturnRatePercent);
            Assert.Null(ra.AvgCompletionHours);

            var rb = report.Rows.Single(r => r.InstructorId == b.Id);
            Assert.Equal(1, rb.CompletedTasks);
            Assert.Equal(2, rb.ApprovedQuestions);
            Assert.Equal(0, rb.ReturnRatePercent);
            Assert.True(rb.AvgCompletionHours.HasValue);

            Assert.Equal(2, report.TotalAssigned);
            Assert.Equal(3, report.TotalApproved);
            Assert.Equal(1, report.TotalReturned);
        }

        [Fact]
        public async Task Report_FiltersByPeriodAndInstructor_AndCountsLateTasks()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            var b = await SeedInstructorAsync(h.Factory, "u-b", "أ. بدر");
            await CreateTaskAsync(h, a, 1, Now.AddHours(-5));   // متأخرة الآن
            await CreateTaskAsync(h, b, 1);
            var svc = CreateReport(h);

            var onlyA = await svc.GetReviewersReportAsync(new ReviewersReportFilter { InstructorId = a.Id });
            Assert.Single(onlyA.Rows);
            Assert.Equal(1, onlyA.Rows[0].LateTasks);
            Assert.Equal(2, onlyA.Instructors.Count);   // قائمة الفلتر لا تتأثر بالمدرب المختار

            var oldPeriod = await svc.GetReviewersReportAsync(new ReviewersReportFilter
            {
                From = new DateTime(2026, 8, 1), To = new DateTime(2026, 9, 1)
            });
            Assert.Empty(oldPeriod.Rows);
            Assert.Equal("2026-08-01", oldPeriod.FromLocal);
        }

        [Fact]
        public async Task Report_ExcludesCancelledTasks_AndSwapsReversedDates()
        {
            var h = Create();
            var a = await SeedInstructorAsync(h.Factory, "u-a", "أ. أحمد");
            var cancelled = await CreateTaskAsync(h, a, 1);
            using (var db = h.Factory.CreateDbContext())
            {
                (await db.QuestionReviewTasks.SingleAsync(t => t.Id == cancelled)).Status = QuestionReviewTaskStatus.Cancelled;
                await db.SaveChangesAsync();
            }

            var report = await CreateReport(h).GetReviewersReportAsync(new ReviewersReportFilter
            {
                From = new DateTime(2026, 10, 10), To = new DateTime(2026, 9, 1)   // معكوسة
            });

            Assert.Empty(report.Rows);
            Assert.Equal("2026-09-01", report.FromLocal);
            Assert.Equal("2026-10-10", report.ToLocal);
        }
    }
}
