using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // RTK-S13.5 — اختبارات SQL Server حقيقية لملحق المحور: الفهارس الفريدة المُصفّاة (محاولة جارية واحدة، وبقاء قاعدة 101/102 كما هي)،
    // الفهرس الفريد (AddendumId, EnrollmentId)، تزامن النبضات وبدء الاختبار والتسليم المزدوج، وترجمة الاستعلامات على CompatibilityLevel(120).
    //
    // التشغيل: QDRAT_TEST_SQL على Connection String لقاعدة اختبار (اسمها يحوي "Test") — بدونه تُتخطّى.
    // يلزم أن يكون سكربت RTK_Addendum.sql (وما قبله) مطبَّقًا على قاعدة الاختبار.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class RemedialTrackSqlServerV3Tests : IAsyncLifetime
    {
        private sealed class RealTz : ITimeZoneService
        {
            public DateTime GetNowUtc() => DateTime.UtcNow;
            public DateTime GetNowSaudi() => DateTime.UtcNow.AddHours(3);
            public DateTime ConvertToSaudi(DateTime utcTime) => utcTime.AddHours(3);
            public DateTime ConvertToUtc(DateTime saudiTime) => saudiTime.AddHours(-3);
        }

        private static readonly RemedialTrackActor Admin = new("rtks7-admin", "مدير الاختبار");
        private static readonly RemedialTrackBatchScope All = RemedialTrackBatchScope.Unrestricted;

        private static RemedialTrackProgressService NewProgress()
            => new(new RtkRealDb.Factory(), TimeProvider.System, new RealTz(), NullLogger<RemedialTrackProgressService>.Instance);

        private static RemedialTrackAddendumStudentService NewStudent()
            => new(new RtkRealDb.Factory(), TimeProvider.System, new RealTz(), NullLogger<RemedialTrackAddendumStudentService>.Instance);

        private static RemedialTrackExamService NewExams()
            => new(new RtkRealDb.Factory(), TimeProvider.System, NewProgress(), NewStudent(), NullLogger<RemedialTrackExamService>.Instance);

        private static RemedialTrackAddendumService NewAdmin()
            => new(new RtkRealDb.Factory(), TimeProvider.System, new Mock<INotificationService>().Object,
                   new Mock<IAdminActivityLogger>().Object, NullLogger<RemedialTrackAddendumService>.Instance);

        public Task InitializeAsync() => RtkRealDb.IsConfigured ? RtkRealDb.CleanupAsync() : Task.CompletedTask;

        public Task DisposeAsync() => RtkRealDb.IsConfigured ? RtkRealDb.CleanupAsync() : Task.CompletedTask;

        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.GetBaseException() is SqlException s && (s.Number == 2601 || s.Number == 2627);

        private static async Task<int> CreateAddendumAsync(RtkSeed seed, int axisIndex = 0, bool withExam = false)
        {
            var r = await NewAdmin().CreateAsync(
                new CreateAddendumInput(seed.PublicationId, seed.AxisIds[axisIndex], "RTKS7 ملحق", "https://youtu.be/ddddddddddd", 100,
                    "سبب إضافة الملحق للاختبار", withExam ? seed.Model101Id : null, withExam ? 15 : null, true, null),
                Admin, All);
            Assert.True(r.Success, r.Message);
            return (int)r.Data!;
        }

        private static RemedialTrackExamAttempt NewAttempt(int axisProgressId, int modelId, RemedialTrackExamNumber number, int? addendumId, RemedialTrackAttemptStatus status)
            => new()
            {
                AxisProgressId = axisProgressId, ExamNumber = number, AddendumId = addendumId, ModelId = modelId, Status = status,
                StartedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
                SubmittedAtUtc = status == RemedialTrackAttemptStatus.InProgress ? null : DateTime.UtcNow, TotalQuestions = 4
            };

        // ───────────── الفهارس ─────────────

        [SqlServerFact]
        public async Task Schema_HasFilteredIndexes_ForAttempts()
        {
            await RtkRealDb.SeedAsync(students: 1);
            await using var db = RtkRealDb.CreateDb();
            var rows = await db.Database.SqlQueryRaw<string>(
                "SELECT name + '|' + CAST(is_unique AS varchar(1)) + '|' + ISNULL(filter_definition, '') AS [Value] FROM sys.indexes " +
                "WHERE object_id = OBJECT_ID(N'RemedialTrackExamAttempts') AND name IN (N'IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber', N'UX_RemedialTrackExamAttempts_Addendum_OpenAttempt')")
                .ToListAsync();
            Assert.Contains(rows, r => r.StartsWith("IX_RemedialTrackExamAttempts_AxisProgressId_ExamNumber|1|") && r.Contains("[AddendumId] IS NULL"));
            Assert.Contains(rows, r => r.StartsWith("UX_RemedialTrackExamAttempts_Addendum_OpenAttempt|1|") && r.Contains("[AddendumId] IS NOT NULL") && r.Contains("[Status]=(0)"));
        }

        [SqlServerFact]
        public async Task OpenAttemptIndex_RejectsSecondInProgressAddendumAttempt_ButAllowsClosedHistory()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1);
            var s = seed.Students[0];
            var addendumId = await CreateAddendumAsync(seed, 0, withExam: true);
            var ap = s.AxisProgressIds[0];

            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackExamAttempts.Add(NewAttempt(ap, seed.Model101Id, RemedialTrackExamNumber.Addendum, addendumId, RemedialTrackAttemptStatus.InProgress));
                await db.SaveChangesAsync();
            }

            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackExamAttempts.Add(NewAttempt(ap, seed.Model101Id, RemedialTrackExamNumber.Addendum, addendumId, RemedialTrackAttemptStatus.InProgress));
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.True(IsUniqueViolation(ex), "يجب أن يرفض الفهرس المُصفّى محاولة جارية ثانية");
            }

            // التاريخ المُغلق غير محدود (محاولات متعددة مُسلَّمة لنفس الملحق/المحور)
            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackExamAttempts.AddRange(
                    NewAttempt(ap, seed.Model101Id, RemedialTrackExamNumber.Addendum, addendumId, RemedialTrackAttemptStatus.Submitted),
                    NewAttempt(ap, seed.Model101Id, RemedialTrackExamNumber.Addendum, addendumId, RemedialTrackAttemptStatus.Expired),
                    NewAttempt(ap, seed.Model101Id, RemedialTrackExamNumber.Addendum, addendumId, RemedialTrackAttemptStatus.Submitted));
                await db.SaveChangesAsync();
            }
        }

        [SqlServerFact]
        public async Task AxisExamIndex_StillEnforcesOneAttemptPerExam_AndIgnoresAddendumAttempts()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1);
            var ap = seed.Students[0].AxisProgressIds[0];
            var addendumId = await CreateAddendumAsync(seed, 0, withExam: true);

            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackExamAttempts.Add(NewAttempt(ap, seed.Model101Id, RemedialTrackExamNumber.Exam101, null, RemedialTrackAttemptStatus.Submitted));
                // محاولة ملحق لنفس المحور لا تتعارض مع 101
                db.RemedialTrackExamAttempts.Add(NewAttempt(ap, seed.Model101Id, RemedialTrackExamNumber.Addendum, addendumId, RemedialTrackAttemptStatus.Submitted));
                await db.SaveChangesAsync();
            }

            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackExamAttempts.Add(NewAttempt(ap, seed.Model101Id, RemedialTrackExamNumber.Exam101, null, RemedialTrackAttemptStatus.Submitted));
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.True(IsUniqueViolation(ex), "قاعدة «محاولة واحدة لاختبار 101» يجب أن تبقى كما هي");
            }
        }

        [SqlServerFact]
        public async Task AddendumProgress_UniquePerAddendumAndEnrollment()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1);
            var addendumId = await CreateAddendumAsync(seed);

            await using var db = RtkRealDb.CreateDb();
            db.RemedialTrackAddendumProgresses.Add(new RemedialTrackAddendumProgress { AddendumId = addendumId, EnrollmentId = seed.Students[0].EnrollmentId });
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.True(IsUniqueViolation(ex));
        }

        // ───────────── الإنشاء والاستعلامات على SQL Server ─────────────

        [SqlServerFact]
        public async Task Create_ForManyStudents_InsertsOneRowEach_AndAllQueriesTranslate()
        {
            var seed = await RtkRealDb.SeedAsync(students: 25);
            var addendumId = await CreateAddendumAsync(seed, 0, withExam: true);

            await using (var db = RtkRealDb.CreateDb())
                Assert.Equal(25, await db.RemedialTrackAddendumProgresses.CountAsync(p => p.AddendumId == addendumId));

            // نفس الطلب مرة ثانية على ملحق آخر (الفريد (AddendumId, EnrollmentId) لا يتعارض بين ملحقين)
            var second = await CreateAddendumAsync(seed, 1);
            Assert.NotEqual(addendumId, second);

            // كل مسارات القراءة تُترجَم على CompatibilityLevel(120) بلا OPENJSON ولا استثناء
            var admin = NewAdmin();
            var panel = await admin.GetPanelAsync(seed.PublicationId, 1, true, All);
            Assert.Equal(2, panel!.Total);
            Assert.Equal(25, panel.Rows.First(r => r.AddendumId == addendumId).Targeted);
            var students = await admin.GetStudentsAsync(addendumId, 1, All);
            Assert.Equal(25, students!.Total);
            Assert.Equal(AddendumStudentsPage.PageSize, students.Items.Count);
            Assert.Equal(2, (await admin.GetTrackingAsync(seed.PublicationId)).Count);

            var s = seed.Students[0];
            var plan = await NewProgress().GetPlanAsync(s.StudentId, s.EnrollmentId);
            Assert.NotNull(plan);
            Assert.Single(plan!.Addenda);   // الملحق الثاني على المحور 2 (مقفل) لا يظهر بعد
            Assert.NotNull(await NewStudent().GetAsync(s.StudentId, s.EnrollmentId, addendumId));

            await using var db2 = RtkRealDb.CreateDb();
            var report = await RemedialTrackReportBuilder.BuildAsync(db2, s.EnrollmentId, s.StudentId, null, false, DateTime.UtcNow, default);
            Assert.Equal(1, report!.AddendaTotal);   // ملحق المحور المقفل (الثاني) لا يظهر في التقرير حتى يُفتح المحور (D31)
        }

        // ───────────── التزامن ─────────────

        [SqlServerFact]
        public async Task ConcurrentPings_DoNotDoubleCreditServerTime()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1);
            var s = seed.Students[0];
            var addendumId = await CreateAddendumAsync(seed);
            await using var db = RtkRealDb.CreateDb();
            var pid = await db.RemedialTrackAddendumProgresses.Where(p => p.AddendumId == addendumId).Select(p => p.Id).SingleAsync();

            var svc = NewStudent();
            RemedialTrackAddendumPingRequest Req(string st) => new() { EnrollmentId = s.EnrollmentId, AddendumProgressId = pid, State = st, Duration = 100 };

            await svc.RecordPingAsync(s.StudentId, Req("playing"));      // نبضة أولى بلا رصيد
            await Task.Delay(TimeSpan.FromSeconds(3));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => svc.RecordPingAsync(s.StudentId, Req("playing"))));
            sw.Stop();

            Assert.All(results, r => Assert.Equal(RemedialTrackPingStatus.Ok, r.Status));
            await using var check = RtkRealDb.CreateDb();
            var watched = await check.RemedialTrackAddendumProgresses.AsNoTracking().Where(p => p.Id == pid).Select(p => p.WatchedSeconds).SingleAsync();
            // رصيد واحد فقط (≈ زمن الانتظار) لا ثمانية — الشرط LastPingAtUtc == expected يمنع الازدواج
            Assert.InRange(watched, 2.0, 3.0 + sw.Elapsed.TotalSeconds + 1.5);
        }

        [SqlServerFact]
        public async Task ConcurrentExamStart_CreatesExactlyOneOpenAttempt()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1);
            var s = seed.Students[0];
            var addendumId = await CreateAddendumAsync(seed, 0, withExam: true);

            await using (var db = RtkRealDb.CreateDb())
                await db.RemedialTrackAddendumProgresses.Where(p => p.AddendumId == addendumId)
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.VideoCompleted, true).SetProperty(p => p.VideoCompletedAtUtc, DateTime.UtcNow));

            var svc = NewStudent();
            var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => svc.StartExamAsync(s.StudentId, s.EnrollmentId, addendumId)));

            Assert.All(results, r => Assert.True(r.Status is RemedialTrackExamStartStatus.Created or RemedialTrackExamStartStatus.Existing, r.Status.ToString()));
            Assert.Single(results.Select(r => r.AttemptId).Distinct());

            await using var check = RtkRealDb.CreateDb();
            Assert.Equal(1, await check.RemedialTrackExamAttempts.CountAsync(a => a.AddendumId == addendumId && a.Status == RemedialTrackAttemptStatus.InProgress));
        }

        [SqlServerFact]
        public async Task ConcurrentDoubleSubmit_CountsOnce_AndAxisStateIsUntouched()
        {
            // المحور 1 بانتظار اختبار 102: أسوأ حالة للخطر R8 (كان محاولة الملحق تُعامل كأنها 102)
            var seed = await RtkRealDb.SeedAsync(students: 1, firstAxisStatus: RemedialTrackAxisStatus.AwaitingExam102);
            var s = seed.Students[0];
            var addendumId = await CreateAddendumAsync(seed, 0, withExam: true);
            await using (var db = RtkRealDb.CreateDb())
                await db.RemedialTrackAddendumProgresses.Where(p => p.AddendumId == addendumId)
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.VideoCompleted, true));

            var start = await NewStudent().StartExamAsync(s.StudentId, s.EnrollmentId, addendumId);
            Assert.Equal(RemedialTrackExamStartStatus.Created, start.Status);

            // إجابات صحيحة كلها
            var exams = NewExams();
            await using (var db = RtkRealDb.CreateDb())
            {
                var qids = await db.RemedialTrackExamAttemptQuestions.Where(x => x.AttemptId == start.AttemptId).Select(x => x.QuestionId).ToListAsync();
                foreach (var q in qids)
                    Assert.Equal(RemedialTrackSaveAnswerStatus.Ok, (await exams.SaveAnswerAsync(s.StudentId, start.AttemptId, q, RtkRealDb.CorrectAnswer)).Status);
            }

            string AxisState(IEnumerable<RemedialTrackAxisProgress> rows) =>
                string.Join(";", rows.OrderBy(a => a.Order).Select(a => $"{a.Id}|{a.Status}|{a.Round}|{a.Exam101Percent}|{a.Exam102Percent}"));
            string before;
            await using (var db = RtkRealDb.CreateDb())
                before = AxisState(await db.RemedialTrackAxisProgresses.AsNoTracking().Where(a => a.EnrollmentId == s.EnrollmentId).ToListAsync());

            var submits = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => exams.SubmitAsync(s.StudentId, start.AttemptId)));
            Assert.All(submits, r => Assert.True(r.Status is RemedialTrackSubmitStatus.Submitted or RemedialTrackSubmitStatus.AlreadyClosed));

            await using var check = RtkRealDb.CreateDb();
            var p = await check.RemedialTrackAddendumProgresses.AsNoTracking().SingleAsync(x => x.AddendumId == addendumId);
            Assert.Equal(1, p.AttemptsCount);
            Assert.True(p.ExamPassed);
            Assert.NotNull(p.CompletedAtUtc);
            Assert.Equal(before, AxisState(await check.RemedialTrackAxisProgresses.AsNoTracking().Where(a => a.EnrollmentId == s.EnrollmentId).ToListAsync()));
            Assert.Equal(0, await check.RemedialTrackEvents.CountAsync(e => e.EnrollmentId == s.EnrollmentId && (e.Type == RemedialTrackEventType.ExamPassed || e.Type == RemedialTrackEventType.ExamFailed)));
        }
    }
}
