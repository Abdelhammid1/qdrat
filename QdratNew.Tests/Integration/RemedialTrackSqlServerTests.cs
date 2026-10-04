using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // RTK-S7.3 — اختبارات SQL Server حقيقية لما لا يثبته InMemory: الفهارس الفريدة (المُصفّاة وغيرها)،
    // ExecuteUpdateAsync تحت UseCompatibilityLevel(120)، RowVersion، والتسليم المزدوج المتزامن.
    //
    // التشغيل: QDRAT_TEST_SQL على Connection String لقاعدة اختبار (اسمها يحوي "Test") — بدونه تُتخطّى.
    // ملاحظة: الخدمات تُبنى بالإعداد نفسه المستعمل في الإنتاج (EnableRetryOnFailure + UseCompatibilityLevel(120)).
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class RemedialTrackSqlServerTests : IAsyncLifetime
    {
        private sealed class RealTz : ITimeZoneService
        {
            public DateTime GetNowUtc() => DateTime.UtcNow;
            public DateTime GetNowSaudi() => DateTime.UtcNow.AddHours(3);
            public DateTime ConvertToSaudi(DateTime utcTime) => utcTime.AddHours(3);
            public DateTime ConvertToUtc(DateTime saudiTime) => saudiTime.AddHours(-3);
        }

        private static RemedialTrackProgressService NewProgress()
            => new(new RtkRealDb.Factory(), TimeProvider.System, new RealTz(), NullLogger<RemedialTrackProgressService>.Instance);

        private static RemedialTrackExamService NewExams()
            => new(new RtkRealDb.Factory(), TimeProvider.System, NewProgress(), NullLogger<RemedialTrackExamService>.Instance);

        public Task InitializeAsync() => RtkRealDb.IsConfigured ? RtkRealDb.CleanupAsync() : Task.CompletedTask;

        public Task DisposeAsync() => RtkRealDb.IsConfigured ? RtkRealDb.CleanupAsync() : Task.CompletedTask;

        // رقم خطأ SQL الفعلي: 2601/2627 = انتهاك فهرس/قيد فريد
        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.GetBaseException() is SqlException s && (s.Number == 2601 || s.Number == 2627);

        // ───────────── الفهارس الفريدة ─────────────

        [SqlServerFact]
        public async Task ActiveCode_IsUniqueAmongActivePublications_AndReusableAfterCancel()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1, inPerson: true);

            RemedialTrackPublication Clone() => new()
            {
                TrackId = seed.TrackId, BatchId = seed.BatchId, Scope = RemedialTrackPublicationScope.WholeBatch,
                Mode = RemedialTrackDeliveryMode.InPerson, PublishAtUtc = DateTime.UtcNow, Status = RemedialTrackPublicationStatus.Active,
                AccessCode = seed.AccessCode, CodeVersion = 1, CreatedByUserId = "rtks7-admin", CreatedAtUtc = DateTime.UtcNow, TotalStudents = 0
            };

            // نشران نشطان بنفس الرقم ← استثناء فهرس فريد
            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackPublications.Add(Clone());
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.True(IsUniqueViolation(ex), ex.GetBaseException().Message);
            }

            // بعد Cancel للأول يُسمح بإعادة استخدام الرقم
            await using (var db = RtkRealDb.CreateDb())
            {
                await db.RemedialTrackPublications.Where(p => p.Id == seed.PublicationId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, RemedialTrackPublicationStatus.Cancelled));
                db.RemedialTrackPublications.Add(Clone());
                await db.SaveChangesAsync();
            }

            // أوامر النشر بلا رقم (أونلاين) لا تتعارض مهما تعددت (الفهرس مُصفّى بـ AccessCode IS NOT NULL)
            await using (var db = RtkRealDb.CreateDb())
            {
                for (var i = 0; i < 2; i++)
                {
                    var p = Clone();
                    p.AccessCode = null;
                    p.Mode = RemedialTrackDeliveryMode.Online;
                    db.RemedialTrackPublications.Add(p);
                }
                await db.SaveChangesAsync();
            }
        }

        [SqlServerFact]
        public async Task UniqueIndexes_RejectDuplicates_OnEnrollmentAttemptVideoProgressAndAttemptQuestion()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1, firstAxisStatus: RemedialTrackAxisStatus.AwaitingExam101);
            var s = seed.Students[0];

            // (PublicationId, StudentId)
            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackEnrollments.Add(new RemedialTrackEnrollment
                    { PublicationId = seed.PublicationId, TrackId = seed.TrackId, StudentId = s.StudentId, CreatedAtUtc = DateTime.UtcNow });
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.True(IsUniqueViolation(ex), ex.GetBaseException().Message);
            }

            // (AxisProgressId, VideoId, Round)
            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackVideoProgresses.Add(new RemedialTrackVideoProgress
                    { AxisProgressId = s.AxisProgressIds[0], VideoId = seed.VideoIds[0], VideoOrder = 1, Round = 1 });
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.True(IsUniqueViolation(ex), ex.GetBaseException().Message);
            }

            // (AxisProgressId, ExamNumber) و (AttemptId, QuestionId) — عبر الخدمة الحقيقية
            var exams = NewExams();
            var start = await exams.StartExamAsync(s.StudentId, s.EnrollmentId, s.AxisProgressIds[0]);
            Assert.Equal(RemedialTrackExamStartStatus.Created, start.Status);

            await using (var db = RtkRealDb.CreateDb())
            {
                var existing = await db.RemedialTrackExamAttempts.AsNoTracking().SingleAsync(a => a.Id == start.AttemptId);
                db.RemedialTrackExamAttempts.Add(new RemedialTrackExamAttempt
                {
                    AxisProgressId = existing.AxisProgressId, ExamNumber = existing.ExamNumber, ModelId = existing.ModelId,
                    StartedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
                });
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.True(IsUniqueViolation(ex), ex.GetBaseException().Message);
            }

            // بدء مكرر للمحاولة نفسها يعيد Existing (لا استثناء ولا محاولة ثانية)
            var again = await exams.StartExamAsync(s.StudentId, s.EnrollmentId, s.AxisProgressIds[0]);
            Assert.Equal(RemedialTrackExamStartStatus.Existing, again.Status);
            Assert.Equal(start.AttemptId, again.AttemptId);

            await using (var db = RtkRealDb.CreateDb())
            {
                var q = await db.RemedialTrackExamAttemptQuestions.AsNoTracking().FirstAsync(x => x.AttemptId == start.AttemptId);
                db.RemedialTrackExamAttemptQuestions.Add(new RemedialTrackExamAttemptQuestion
                    { AttemptId = start.AttemptId, QuestionId = q.QuestionId, Order = 99 });
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.True(IsUniqueViolation(ex), ex.GetBaseException().Message);
            }
        }

        // ───────────── النبضات: ExecuteUpdateAsync + التزامن ─────────────

        [SqlServerFact]
        public async Task VideoPing_ExecuteUpdate_WorksUnderCompatibilityLevel120()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1);
            var s = seed.Students[0];
            var progress = NewProgress();

            var r = await progress.RecordPingAsync(s.StudentId, new RemedialTrackVideoPingRequest
                { EnrollmentId = s.EnrollmentId, VideoProgressId = s.FirstAxisVideoProgressIds[0], State = "playing", Position = 1, Duration = 100 });

            Assert.Equal(RemedialTrackPingStatus.Ok, r.Status);
            Assert.True(r.Body.Ok);

            await using var db = RtkRealDb.CreateDb();
            var vp = await db.RemedialTrackVideoProgresses.AsNoTracking().SingleAsync(x => x.Id == s.FirstAxisVideoProgressIds[0]);
            Assert.NotNull(vp.LastPingAtUtc);
            Assert.NotNull(vp.FirstPingAtUtc);
            Assert.Equal("playing", vp.LastPingState);
        }

        [SqlServerFact]
        public async Task VideoPing_ConcurrentPingsOnSameVideo_NeverThrow_AndNeverDoubleCredit()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1);
            var s = seed.Students[0];
            var req = new RemedialTrackVideoPingRequest
                { EnrollmentId = s.EnrollmentId, VideoProgressId = s.FirstAxisVideoProgressIds[0], State = "playing", Position = 1, Duration = 100 };

            // نبضة أولى تثبّت LastPingAt، ثم 12 نبضة متزامنة بخدمات مستقلة (مثل 12 تبويبًا)
            Assert.Equal(RemedialTrackPingStatus.Ok, (await NewProgress().RecordPingAsync(s.StudentId, req)).Status);
            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => NewProgress().RecordPingAsync(s.StudentId, req))));

            Assert.All(results, r => Assert.Equal(RemedialTrackPingStatus.Ok, r.Status));

            await using var db = RtkRealDb.CreateDb();
            var vp = await db.RemedialTrackVideoProgresses.AsNoTracking().SingleAsync(x => x.Id == s.FirstAxisVideoProgressIds[0]);
            // الرصيد بزمن الخادم فقط: لا يتجاوز الزمن الفعلي المنقضي (ثوانٍ قليلة) رغم 13 نبضة
            Assert.True(vp.WatchedSeconds < 30, $"credit={vp.WatchedSeconds}");
        }

        // ───────────── RowVersion ─────────────

        [SqlServerFact]
        public async Task AxisProgress_RowVersion_DetectsConcurrentUpdate()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1);
            var apId = seed.Students[0].AxisProgressIds[0];

            await using var a = RtkRealDb.CreateDb();
            await using var b = RtkRealDb.CreateDb();
            var ap1 = await a.RemedialTrackAxisProgresses.FirstAsync(x => x.Id == apId);
            var ap2 = await b.RemedialTrackAxisProgresses.FirstAsync(x => x.Id == apId);

            ap1.Status = RemedialTrackAxisStatus.AwaitingExam101;
            await a.SaveChangesAsync();

            ap2.Status = RemedialTrackAxisStatus.Rewatch;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
        }

        [SqlServerFact]
        public async Task Enrollment_RowVersion_DetectsConcurrentUpdate()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1);
            var id = seed.Students[0].EnrollmentId;

            await using var a = RtkRealDb.CreateDb();
            await using var b = RtkRealDb.CreateDb();
            var e1 = await a.RemedialTrackEnrollments.FirstAsync(x => x.Id == id);
            var e2 = await b.RemedialTrackEnrollments.FirstAsync(x => x.Id == id);

            e1.FailedCodeAttempts = 1;
            await a.SaveChangesAsync();

            e2.FailedCodeAttempts = 2;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
        }

        // ───────────── التسليم المزدوج المتزامن (Idempotent) ─────────────

        [SqlServerFact]
        public async Task ConcurrentDoubleSubmit_TransitionsOnce_AndScoresOnce()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1, firstAxisStatus: RemedialTrackAxisStatus.AwaitingExam101);
            var s = seed.Students[0];
            var exams = NewExams();

            var start = await exams.StartExamAsync(s.StudentId, s.EnrollmentId, s.AxisProgressIds[0]);
            Assert.Equal(RemedialTrackExamStartStatus.Created, start.Status);

            List<Guid> qids;
            await using (var db = RtkRealDb.CreateDb())
                qids = await db.RemedialTrackExamAttemptQuestions.AsNoTracking()
                    .Where(x => x.AttemptId == start.AttemptId).OrderBy(x => x.Order).Select(x => x.QuestionId).ToListAsync();
            foreach (var q in qids)
                Assert.Equal(RemedialTrackSaveAnswerStatus.Ok, (await exams.SaveAnswerAsync(s.StudentId, start.AttemptId, q, RtkRealDb.CorrectAnswer)).Status);

            var results = await Task.WhenAll(Enumerable.Range(0, 6)
                .Select(_ => Task.Run(() => NewExams().SubmitAsync(s.StudentId, start.AttemptId))));

            Assert.All(results, r => Assert.True(
                r.Status is RemedialTrackSubmitStatus.Submitted or RemedialTrackSubmitStatus.AlreadyClosed, $"status={r.Status}"));
            Assert.Contains(results, r => r.Status == RemedialTrackSubmitStatus.Submitted);

            await using var check = RtkRealDb.CreateDb();
            var attempt = await check.RemedialTrackExamAttempts.AsNoTracking().SingleAsync(a => a.Id == start.AttemptId);
            Assert.Equal(RemedialTrackAttemptStatus.Submitted, attempt.Status);
            Assert.True(attempt.IsPassed);
            Assert.Equal(100d, attempt.ScorePercent);

            var ap = await check.RemedialTrackAxisProgresses.AsNoTracking().SingleAsync(a => a.Id == s.AxisProgressIds[0]);
            Assert.Equal(RemedialTrackAxisStatus.Passed, ap.Status);

            // المحور التالي فُتح مرة واحدة فقط وحدث النجاح سُجّل مرة واحدة
            Assert.Equal(1, await check.RemedialTrackEvents.CountAsync(e => e.EnrollmentId == s.EnrollmentId && e.Type == RemedialTrackEventType.ExamPassed));
            Assert.Equal(1, await check.RemedialTrackEvents.CountAsync(e => e.EnrollmentId == s.EnrollmentId && e.Type == RemedialTrackEventType.AxisOpened));
        }
    }
}
