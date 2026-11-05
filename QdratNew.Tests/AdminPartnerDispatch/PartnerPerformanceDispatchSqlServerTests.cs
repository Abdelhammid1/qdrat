using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Implementations;
using QdratNew.Services.PartnerDispatch;
using QdratNew.Services.PerformanceIndicator;
using QdratNew.Services.PerformanceIndicator.PartnerScope;
using QdratNew.Tests.Integration;
using QdratNew.ViewModels.Partner.PerformanceIndicator;
using Xunit;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S4.3 — على قاعدة حقيقية: النسخ متطابق الأسئلة، عزل الشركاء، الطلاب النشطون، عزل الفشل،
    // السجل، الضغطة المزدوجة، وإعادة الإرسال التي لا تحذف نتائج.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class PartnerPerformanceDispatchSqlServerTests : IAsyncLifetime
    {
        private static readonly DispatchActor Actor = new(ApdTestData.SenderId, "مرسل الاختبار");

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured)
                return;

            await using var db = RtkRealDb.CreateDb();
            await ApdTestData.CleanupAsync(db);
        }

        public async Task DisposeAsync()
        {
            if (!RtkRealDb.IsConfigured)
                return;

            await using var db = RtkRealDb.CreateDb();
            await ApdTestData.CleanupAsync(db);
        }

        private static async Task<(ApdSeed Seed, ApdPerformanceSeed Kpi)> SeedAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            var seed = await ApdTestData.SeedAsync(db);
            return (seed, await ApdTestData.PreparePerformanceAsync(db, seed));
        }

        private static PartnerPerformanceDispatchService NewDispatch(ApplicationDbContext db, IPartnerDispatchLogService? log = null)
            => new(
                db,
                log ?? new PartnerDispatchLogService(db),
                new AdvancedNotificationService(db),
                NullLogger<PartnerPerformanceDispatchService>.Instance);

        private static PartnerPerformanceExamService NewScope(ApplicationDbContext db)
            => new(db, new AdvancedNotificationService(db), NullLogger<PartnerPerformanceExamService>.Instance);

        private sealed class ThrowingForPartnerLog : IPartnerDispatchLogService
        {
            private readonly int _partnerId;
            private readonly IPartnerDispatchLogService _inner;

            public ThrowingForPartnerLog(int partnerId, IPartnerDispatchLogService inner)
            {
                _partnerId = partnerId;
                _inner = inner;
            }

            public Task WriteAsync(IReadOnlyCollection<PartnerDispatchLog> entries, CancellationToken ct = default)
                => entries.Any(e => e.PartnerId == _partnerId)
                    ? throw new InvalidOperationException("فشل السجل")
                    : _inner.WriteAsync(entries, ct);

            public IQueryable<PartnerDispatchLog> Query() => _inner.Query();
        }

        private static PartnerDispatchTarget Target(int partnerId, string name, params int[] batchIds)
            => new() { PartnerId = partnerId, PartnerName = name, BatchIds = batchIds.ToList() };

        private static async Task<int> SendToAAsync(ApdSeed seed, ApdPerformanceSeed kpi)
        {
            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).DispatchAsync(kpi.TemplateId, new[] { Target(seed.PartnerA, "A", seed.BatchA1) }, Actor);
            Assert.True(outcome.Results.Single().Succeeded, outcome.Results.Single().Message);

            return await db.PerformanceIndicatorExams.AsNoTracking()
                .Where(e => e.OwnerPartnerId == seed.PartnerA)
                .Select(e => e.Id)
                .SingleAsync();
        }

        [SqlServerFact]
        public async Task Dispatch_CreatesSeparateCopyPerSchool_WithIdenticalQuestions_AndOwnReferenceCode()
        {
            var (seed, kpi) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).DispatchAsync(
                kpi.TemplateId,
                new[] { Target(seed.PartnerA, "A", seed.BatchA1), Target(seed.PartnerB, "B", seed.BatchB1) },
                Actor);

            Assert.Equal(2, outcome.SucceededCount);

            var copyA = await db.PerformanceIndicatorExams.AsNoTracking().SingleAsync(e => e.OwnerPartnerId == seed.PartnerA);
            var copyB = await db.PerformanceIndicatorExams.AsNoTracking().SingleAsync(e => e.OwnerPartnerId == seed.PartnerB);
            var template = await db.PerformanceIndicatorExams.AsNoTracking().SingleAsync(e => e.Id == kpi.TemplateId);

            Assert.NotEqual(copyA.Id, copyB.Id);
            Assert.Equal(new[] { template.ReferenceCode, copyA.ReferenceCode, copyB.ReferenceCode }.Distinct().Count(), 3);

            Task<List<(Guid, int?, int)>> QuestionsAsync(int examId) => db.PerformanceIndicatorExamQuestions.AsNoTracking()
                .Where(q => q.PerformanceIndicatorExamId == examId)
                .OrderBy(q => q.OrderNumber)
                .Select(q => new ValueTuple<Guid, int?, int>(q.QuestionId, q.SectionId, q.OrderNumber))
                .ToListAsync();

            var original = await QuestionsAsync(kpi.TemplateId);
            Assert.Equal(2, original.Count);
            Assert.Equal(original, await QuestionsAsync(copyA.Id));
            Assert.Equal(original, await QuestionsAsync(copyB.Id));

            Assert.Equal(1, await db.PerformanceIndicatorExamSections.AsNoTracking().CountAsync(s => s.PerformanceIndicatorExamId == copyA.Id && s.SectionId == kpi.SectionId));

            foreach (var copy in new[] { copyA, copyB })
            {
                Assert.True(copy.IsSent);
                Assert.False(copy.IsArchived);
                Assert.False(copy.IsPartnerTemplate);
                Assert.Equal(template.CurriculumId, copy.CurriculumId);
                Assert.Equal(template.TotalQuestions, copy.TotalQuestions);
                Assert.Equal(template.DurationMinutes, copy.DurationMinutes);
                Assert.Equal(template.PassPercent, copy.PassPercent);
            }

            // القالب لا يصل لأي طالب ولا دفعة
            Assert.False(template.IsSent);
            Assert.True(template.IsArchived);
            Assert.Null(template.OwnerPartnerId);
            Assert.False(await db.PerformanceIndicatorExamStudents.AsNoTracking().AnyAsync(s => s.PerformanceIndicatorExamId == kpi.TemplateId));
            Assert.False(await db.PerformanceIndicatorExamToBatch.AsNoTracking().AnyAsync(s => s.PerformanceIndicatorExamId == kpi.TemplateId));
        }

        [SqlServerFact]
        public async Task Dispatch_ReachesOnlyActiveStudentsOfTheSchool_AndWritesLogPerBatch()
        {
            var (seed, kpi) = await SeedAsync();
            var examId = await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();

            // A1 فيها: نشط، غير نشط، طالب مدرسة أخرى، منسحب
            var students = await db.PerformanceIndicatorExamStudents.AsNoTracking()
                .Where(s => s.PerformanceIndicatorExamId == examId)
                .Select(s => s.StudentId)
                .ToListAsync();
            Assert.Equal(new[] { seed.ActiveStudentA }, students);

            var log = await db.PartnerDispatchLogs.AsNoTracking().SingleAsync(l => l.PartnerId == seed.PartnerA);
            Assert.Equal(PartnerDispatchKind.PerformanceIndicator, log.Kind);
            Assert.Equal(kpi.TemplateId, log.SourcePerformanceIndicatorExamId);
            Assert.Equal(examId, log.TargetEntityId);
            Assert.Equal(seed.BatchA1, log.BatchId);
            Assert.Equal(1, log.StudentsCount);
            Assert.False(log.ContractOverridden);
        }

        [SqlServerFact]
        public async Task Dispatch_FailureOfOneSchool_DoesNotCancelTheOther_AndLeavesNothingForIt()
        {
            var (seed, kpi) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var log = new ThrowingForPartnerLog(seed.PartnerA, new PartnerDispatchLogService(db));

            var outcome = await NewDispatch(db, log).DispatchAsync(
                kpi.TemplateId,
                new[] { Target(seed.PartnerA, "A", seed.BatchA1), Target(seed.PartnerB, "B", seed.BatchB1) },
                Actor);

            Assert.False(outcome.Results.Single(r => r.PartnerId == seed.PartnerA).Succeeded);
            Assert.True(outcome.Results.Single(r => r.PartnerId == seed.PartnerB).Succeeded);

            Assert.False(await db.PerformanceIndicatorExams.AsNoTracking().AnyAsync(e => e.OwnerPartnerId == seed.PartnerA));
            Assert.True(await db.PerformanceIndicatorExams.AsNoTracking().AnyAsync(e => e.OwnerPartnerId == seed.PartnerB));
        }

        [SqlServerFact]
        public async Task Dispatch_ForeignBatch_FailsForThatSchoolOnly()
        {
            var (seed, kpi) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).DispatchAsync(
                kpi.TemplateId,
                new[] { Target(seed.PartnerA, "A", seed.BatchB1), Target(seed.PartnerB, "B", seed.BatchB1) },
                Actor);

            Assert.Contains("لا تخص", outcome.Results.Single(r => r.PartnerId == seed.PartnerA).Message);
            Assert.True(outcome.Results.Single(r => r.PartnerId == seed.PartnerB).Succeeded);
            Assert.False(await db.PerformanceIndicatorExams.AsNoTracking().AnyAsync(e => e.OwnerPartnerId == seed.PartnerA));
        }

        [SqlServerFact]
        public async Task Dispatch_Override_IsLogged_AndWarnsThatFollowUpNeedsContract()
        {
            var (seed, kpi) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var target = new PartnerDispatchTarget
            {
                PartnerId = seed.PartnerA, PartnerName = "A", BatchIds = new List<int> { seed.BatchA1 },
                ContractOverridden = true, OverrideReason = "اتفاق خاص مع إدارة المدرسة"
            };

            var outcome = await NewDispatch(db).DispatchAsync(kpi.TemplateId, new[] { target }, Actor);

            var log = await db.PartnerDispatchLogs.AsNoTracking().SingleAsync(l => l.PartnerId == seed.PartnerA);
            Assert.True(log.ContractOverridden);
            Assert.Equal("اتفاق خاص مع إدارة المدرسة", log.OverrideReason);
            Assert.Contains("لا يشمل", outcome.Results.Single().Message);
        }

        [SqlServerFact]
        public async Task Dispatch_SecondClickWithinWindow_DoesNotCreateSecondCopy()
        {
            var (seed, kpi) = await SeedAsync();
            await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).DispatchAsync(kpi.TemplateId, new[] { Target(seed.PartnerA, "A", seed.BatchA1) }, Actor);

            Assert.False(outcome.Results.Single().Succeeded);
            Assert.Contains("قبل لحظات", outcome.Results.Single().Message);
            Assert.Equal(1, await db.PerformanceIndicatorExams.AsNoTracking().CountAsync(e => e.OwnerPartnerId == seed.PartnerA));
        }

        [SqlServerFact]
        public async Task Dispatch_NotATemplate_IsNotFound_NothingWritten()
        {
            var (seed, kpi) = await SeedAsync();
            var copyId = await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).DispatchAsync(copyId, new[] { Target(seed.PartnerB, "B", seed.BatchB1) }, Actor);

            Assert.True(outcome.IsNotFound);
            Assert.False(await db.PerformanceIndicatorExams.AsNoTracking().AnyAsync(e => e.OwnerPartnerId == seed.PartnerB));
        }

        // ---------------- الخدمة بنطاق المدرسة ----------------
        [SqlServerFact]
        public async Task Scope_OtherSchool_AndInstituteExam_AreNotVisible()
        {
            var (seed, kpi) = await SeedAsync();
            var examId = await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();
            var scope = NewScope(db);

            Assert.Empty((await scope.GetIndexAsync(seed.PartnerB, false, true)).Exams);
            Assert.Null(await scope.GetStudentsAsync(seed.PartnerB, examId));
            Assert.Null(await scope.GetStudentQuestionsAsync(seed.PartnerB, examId, seed.ActiveStudentA));
            Assert.Null(await scope.GetBatchReportAsync(seed.PartnerB, examId, seed.BatchA1));
            Assert.Equal(PartnerPerformanceResendOutcome.NotFound, await scope.ResendToStudentAsync(seed.PartnerB, examId, seed.ActiveStudentA));
            Assert.False(await scope.SetHiddenAsync(seed.PartnerB, examId, true, "x"));

            // القالب ملك المعهد
            Assert.Null(await scope.GetStudentsAsync(seed.PartnerA, kpi.TemplateId));

            var own = Assert.Single((await scope.GetIndexAsync(seed.PartnerA, false, true)).Exams);
            Assert.Equal(examId, own.Id);
            Assert.Equal(1, own.StudentsCount);
        }

        [SqlServerFact]
        public async Task Scope_SelfEnrolledForeignStudent_IsNotShownToTheSchool()
        {
            var (seed, kpi) = await SeedAsync();
            var examId = await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();
            db.PerformanceIndicatorExamStudents.Add(new PerformanceIndicatorExamStudent { PerformanceIndicatorExamId = examId, StudentId = kpi.StudentB });
            await db.SaveChangesAsync();

            var students = await NewScope(db).GetStudentsAsync(seed.PartnerA, examId);

            Assert.Equal(new[] { seed.ActiveStudentA }, students!.Report.Students.Select(s => s.StudentId));
        }

        [SqlServerFact]
        public async Task StudentQuestions_OpenOnlyAfterStudentCompletes()
        {
            var (seed, kpi) = await SeedAsync();
            var examId = await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();
            var scope = NewScope(db);

            var before = await scope.GetStudentQuestionsAsync(seed.PartnerA, examId, seed.ActiveStudentA);
            Assert.False(before!.StudentCompleted);
            Assert.Null(before.Report);

            await db.PerformanceIndicatorExamStudents
                .Where(s => s.PerformanceIndicatorExamId == examId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.IsCompleted, true));

            var after = await scope.GetStudentQuestionsAsync(seed.PartnerA, examId, seed.ActiveStudentA);
            Assert.True(after!.StudentCompleted);
            Assert.Equal(2, after.Report!.TotalQuestions);
        }

        [SqlServerFact]
        public async Task BatchReport_UsesCompletedStudentsOnly_AndComputesSectionAverage()
        {
            var (seed, kpi) = await SeedAsync();
            var examId = await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();
            await db.PerformanceIndicatorExamStudents
                .Where(s => s.PerformanceIndicatorExamId == examId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.IsCompleted, true));

            db.QuestionAttemptNew.Add(new QuestionAttemptNew
            {
                StudentId = seed.ActiveStudentA, QuestionId = kpi.QuestionA, PerformanceIndicatorExamId = examId,
                SelectedAnswer = "أ", IsCorrect = true
            });
            await db.SaveChangesAsync();

            var report = await NewScope(db).GetBatchReportAsync(seed.PartnerA, examId, seed.BatchA1);

            Assert.Equal(1, report!.TotalStudents);
            Assert.Equal(1, report.CompletedStudents);
            Assert.Equal(1, report.PassedStudents); // 50% ومعيار الاجتياز 50
            Assert.Equal(50, report.AverageScore);
            Assert.Equal(50, Assert.Single(report.Sections).AverageScore);

            // دفعة لا يربطها الاختبار
            Assert.Null(await NewScope(db).GetBatchReportAsync(seed.PartnerA, examId, seed.BatchAEmpty));
        }

        // ---------------- إعادة الإرسال ----------------
        [SqlServerFact]
        public async Task Resend_CompletedStudent_KeepsResultAndAnswers()
        {
            var (seed, kpi) = await SeedAsync();
            var examId = await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();
            var startedAt = DateTime.UtcNow.AddHours(-1);
            await db.PerformanceIndicatorExamStudents
                .Where(s => s.PerformanceIndicatorExamId == examId)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.IsCompleted, true)
                    .SetProperty(s => s.StartedAt, startedAt)
                    .SetProperty(s => s.ScorePercent, 75));

            db.QuestionAttemptNew.Add(new QuestionAttemptNew
            {
                StudentId = seed.ActiveStudentA, QuestionId = kpi.QuestionA, PerformanceIndicatorExamId = examId,
                SelectedAnswer = "أ", IsCorrect = true
            });
            await db.SaveChangesAsync();

            var outcome = await NewScope(db).ResendToStudentAsync(seed.PartnerA, examId, seed.ActiveStudentA);

            Assert.Equal(PartnerPerformanceResendOutcome.AlreadyCompleted, outcome);
            var row = await db.PerformanceIndicatorExamStudents.AsNoTracking().SingleAsync(s => s.PerformanceIndicatorExamId == examId);
            Assert.True(row.IsCompleted);
            Assert.Equal(75, row.ScorePercent);
            Assert.NotNull(row.StartedAt);
            Assert.Equal(1, await db.QuestionAttemptNew.AsNoTracking().CountAsync(a => a.PerformanceIndicatorExamId == examId));
        }

        [SqlServerFact]
        public async Task Resend_StartedStudent_GetsFullTime_AnswersKept()
        {
            var (seed, kpi) = await SeedAsync();
            var examId = await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();
            var startedAt = DateTime.UtcNow.AddMinutes(-1);
            await db.PerformanceIndicatorExamStudents
                .Where(s => s.PerformanceIndicatorExamId == examId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.StartedAt, startedAt));

            db.QuestionAttemptNew.Add(new QuestionAttemptNew
            {
                StudentId = seed.ActiveStudentA, QuestionId = kpi.QuestionA, PerformanceIndicatorExamId = examId,
                SelectedAnswer = "أ", IsCorrect = true
            });
            await db.SaveChangesAsync();

            var outcome = await NewScope(db).ResendToStudentAsync(seed.PartnerA, examId, seed.ActiveStudentA);

            Assert.Equal(PartnerPerformanceResendOutcome.Reopened, outcome);
            var row = await db.PerformanceIndicatorExamStudents.AsNoTracking().SingleAsync(s => s.PerformanceIndicatorExamId == examId);
            Assert.Null(row.StartedAt);
            Assert.False(row.IsCompleted);
            Assert.Equal(1, await db.QuestionAttemptNew.AsNoTracking().CountAsync(a => a.PerformanceIndicatorExamId == examId));
        }

        [SqlServerFact]
        public async Task Resend_NewStudentInBatch_IsAddedOnce()
        {
            var (seed, kpi) = await SeedAsync();
            var examId = await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();
            await db.PerformanceIndicatorExamStudents.Where(s => s.PerformanceIndicatorExamId == examId).ExecuteDeleteAsync();

            var scope = NewScope(db);
            var notAssigned = (await scope.GetStudentsAsync(seed.PartnerA, examId))!.NotAssigned;
            Assert.Equal(new[] { seed.ActiveStudentA }, notAssigned.Select(s => s.StudentId));

            Assert.Equal(PartnerPerformanceResendOutcome.Added, await scope.ResendToStudentAsync(seed.PartnerA, examId, seed.ActiveStudentA));
            Assert.Equal(PartnerPerformanceResendOutcome.AlreadyAvailable, await scope.ResendToStudentAsync(seed.PartnerA, examId, seed.ActiveStudentA));
            Assert.Equal(1, await db.PerformanceIndicatorExamStudents.AsNoTracking().CountAsync(s => s.PerformanceIndicatorExamId == examId));
        }

        [SqlServerFact]
        public async Task Resend_ForeignOrInactiveOrOutOfBatchStudent_IsRefused()
        {
            var (seed, kpi) = await SeedAsync();
            var examId = await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();
            var scope = NewScope(db);

            // طالب مدرسة أخرى
            Assert.Equal(PartnerPerformanceResendOutcome.NotEligible, await scope.ResendToStudentAsync(seed.PartnerA, examId, kpi.StudentB));

            // طالب من المدرسة غير نشط
            var inactive = await db.StudentBatchEnrollments.AsNoTracking()
                .Where(e => e.BatchId == seed.BatchA1 && !e.Student.IsActiveForLearning)
                .Select(e => e.StudentID)
                .FirstAsync();
            Assert.Equal(PartnerPerformanceResendOutcome.NotEligible, await scope.ResendToStudentAsync(seed.PartnerA, examId, inactive));

            // منسحب من الدفعة
            var withdrawn = await db.StudentBatchEnrollments.AsNoTracking()
                .Where(e => e.BatchId == seed.BatchA1 && e.Status == "Withdrawn")
                .Select(e => e.StudentID)
                .FirstAsync();
            Assert.Equal(PartnerPerformanceResendOutcome.NotEligible, await scope.ResendToStudentAsync(seed.PartnerA, examId, withdrawn));

            Assert.Equal(1, await db.PerformanceIndicatorExamStudents.AsNoTracking().CountAsync(s => s.PerformanceIndicatorExamId == examId));
        }

        [SqlServerFact]
        public async Task HideAndRestore_ChangeSchoolListOnly_StudentRowStays()
        {
            var (seed, kpi) = await SeedAsync();
            var examId = await SendToAAsync(seed, kpi);

            await using var db = RtkRealDb.CreateDb();
            var scope = NewScope(db);

            Assert.True(await scope.SetHiddenAsync(seed.PartnerA, examId, true, ApdTestData.SenderId));

            var visible = await scope.GetIndexAsync(seed.PartnerA, false, true);
            Assert.Empty(visible.Exams);
            Assert.Equal(1, visible.HiddenCount);
            Assert.Single((await scope.GetIndexAsync(seed.PartnerA, true, true)).Exams);
            Assert.Equal(1, await db.PerformanceIndicatorExamStudents.AsNoTracking().CountAsync(s => s.PerformanceIndicatorExamId == examId));

            Assert.True(await scope.SetHiddenAsync(seed.PartnerA, examId, false, null));
            Assert.Single((await scope.GetIndexAsync(seed.PartnerA, false, true)).Exams);
        }

        // ---------------- توليد القالب ----------------
        [SqlServerFact]
        public async Task CreatePartnerTemplate_GeneratesQuestions_WithoutBatchesOrStudents()
        {
            var (_, kpi) = await SeedAsync();

            var service = new PerformanceIndicatorExamService(new RealDbFactory(), new MemoryCache(new MemoryCacheOptions()));
            var id = await service.CreatePartnerTemplateAsync($"{ApdTestData.Marker} قالب مولّد", kpi.CurriculumId, true, DateTime.Today, DateTime.Today.AddDays(1), 2, ApdTestData.SenderId);

            await using var db = RtkRealDb.CreateDb();
            var exam = await db.PerformanceIndicatorExams.AsNoTracking().SingleAsync(e => e.Id == id);

            Assert.True(exam.IsPartnerTemplate);
            Assert.True(exam.IsArchived);
            Assert.False(exam.IsSent);
            Assert.Null(exam.OwnerPartnerId);
            // التوليد القائم قد يختار نفس السؤال مرتين ثم يزيل التكرار، فالعدد 1 أو 2
            Assert.InRange(exam.TotalQuestions, 1, 2);
            Assert.Equal(exam.TotalQuestions, await db.PerformanceIndicatorExamQuestions.AsNoTracking().CountAsync(q => q.PerformanceIndicatorExamId == id));
            Assert.False(await db.PerformanceIndicatorExamStudents.AsNoTracking().AnyAsync(s => s.PerformanceIndicatorExamId == id));
            Assert.False(string.IsNullOrWhiteSpace(exam.ReferenceCode));
        }

        [SqlServerFact]
        public async Task CreatePartnerTemplate_CurriculumWithNoQualifiedQuestions_SavesNothing()
        {
            var (_, kpi) = await SeedAsync();

            await using (var db = RtkRealDb.CreateDb())
            {
                await db.Questions.Where(q => q.CurriculumId == kpi.CurriculumId)
                    .ExecuteUpdateAsync(u => u.SetProperty(q => q.IsReviewed, false));
            }

            var service = new PerformanceIndicatorExamService(new RealDbFactory(), new MemoryCache(new MemoryCacheOptions()));
            var id = await service.CreatePartnerTemplateAsync($"{ApdTestData.Marker} قالب فارغ", kpi.CurriculumId, true, DateTime.Today, DateTime.Today.AddDays(1), 2, ApdTestData.SenderId);

            Assert.Null(id);

            await using var check = RtkRealDb.CreateDb();
            Assert.False(await check.PerformanceIndicatorExams.AsNoTracking().AnyAsync(e => e.Title == $"{ApdTestData.Marker} قالب فارغ"));
        }

        private sealed class RealDbFactory : IDbContextFactory<ApplicationDbContext>
        {
            public ApplicationDbContext CreateDbContext() => RtkRealDb.CreateDb();
        }

        // ---------------- الرمز المرجعي ----------------
        [Fact]
        public async Task ReferenceCode_SkipsCodesInUse()
        {
            var values = new Queue<int>(new[] { 111111, 222222, 333333 });

            var code = await PerformanceExamReferenceCode.GenerateAsync(
                c => Task.FromResult(c != "333333"),
                () => values.Dequeue());

            Assert.Equal("333333", code);
        }

        [Fact]
        public async Task ReferenceCode_DefaultIsSixDigits()
        {
            var code = await PerformanceExamReferenceCode.GenerateAsync(_ => Task.FromResult(false));
            Assert.Matches("^[1-9][0-9]{5}$", code);
        }

        [Fact]
        public async Task ReferenceCode_AllTaken_Throws()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                PerformanceExamReferenceCode.GenerateAsync(_ => Task.FromResult(true)));
        }
    }
}
