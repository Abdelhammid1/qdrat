using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>
    /// RTK-S4: بوابة الوصول، التحقق من الرقم المرجعي، قائمة الطالب، وخوارزمية نبضة الفيديو.
    /// على InMemory: ExecuteUpdate/الفهارس المُصفّاة/RowVersion لا تُفرض هنا — تُغطّى على SQL Server الحقيقي في RTK-S7.3.
    /// </summary>
    public class RemedialTrackStudentAreaTests
    {
        private static readonly DateTime T0 = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

        private sealed class MutableTime : TimeProvider
        {
            public DateTime Now { get; set; } = T0;
            public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
        }

        private sealed class FakeTz : ITimeZoneService
        {
            public DateTime GetNowUtc() => T0;
            public DateTime GetNowSaudi() => T0.AddHours(3);
            public DateTime ConvertToSaudi(DateTime utcTime) => utcTime.AddHours(3);
            public DateTime ConvertToUtc(DateTime saudiTime) => saudiTime.AddHours(-3);
        }

        private sealed class Fixture
        {
            public TestDbContextFactory Factory { get; } = new(Guid.NewGuid().ToString());
            public MutableTime Clock { get; } = new();
            public RemedialTrackAccessService Access { get; }
            public RemedialTrackProgressService Progress { get; }

            public int StudentA { get; private set; } = 101;
            public int StudentB { get; private set; } = 202;
            public int PublicationId { get; private set; }
            public int EnrollmentA { get; private set; }
            public int EnrollmentB { get; private set; }
            public int Axis1ProgressA { get; private set; }
            public int Axis2ProgressA { get; private set; }

            public Fixture()
            {
                Access = new RemedialTrackAccessService(Factory, Clock);
                Progress = new RemedialTrackProgressService(Factory, Clock, new FakeTz(), NullLogger<RemedialTrackProgressService>.Instance);
            }

            public async Task SeedAsync(
                RemedialTrackDeliveryMode mode = RemedialTrackDeliveryMode.Online,
                DateTime? publishAt = null,
                RemedialTrackPublicationStatus pubStatus = RemedialTrackPublicationStatus.Active)
            {
                await using var db = Factory.CreateDbContext();

                var cur = new Curriculum { Title = "قدرات", Description = "d", CurriculumTypeName = "t" };
                db.Curriculums.Add(cur);
                await db.SaveChangesAsync();

                var track = new RemedialTrack
                {
                    Code = "RTK-2026-0001", Title = "خطة علاجية", CurriculumId = cur.Id, Status = RemedialTrackStatus.Ready,
                    MinWatchPercent = 90, IsStructureLocked = true, CreatedByUserId = "admin", CreatedAtUtc = T0.AddDays(-3)
                };
                db.RemedialTracks.Add(track);
                await db.SaveChangesAsync();

                var sec1 = new Section { Title = "قسم 1", CurriculumId = cur.Id };
                var sec2 = new Section { Title = "قسم 2", CurriculumId = cur.Id };
                db.Sections.AddRange(sec1, sec2);
                await db.SaveChangesAsync();

                var axis1 = new RemedialTrackAxis { TrackId = track.Id, SectionId = sec1.Id, Order = 1, TitleOverride = "محور 1", Exam101ModelId = 1, Exam102ModelId = 2 };
                var axis2 = new RemedialTrackAxis { TrackId = track.Id, SectionId = sec2.Id, Order = 2, TitleOverride = "محور 2", Exam101ModelId = 1, Exam102ModelId = 2 };
                db.RemedialTrackAxes.AddRange(axis1, axis2);
                await db.SaveChangesAsync();

                db.RemedialTrackVideos.AddRange(
                    new RemedialTrackVideo { AxisId = axis1.Id, Order = 1, Title = "فيديو 1", Url = "https://youtu.be/aaaaaaaaaaa", Provider = RemedialTrackVideoProvider.YouTube, ExternalId = "aaaaaaaaaaa", DurationSeconds = 100 },
                    new RemedialTrackVideo { AxisId = axis1.Id, Order = 2, Title = "فيديو 2", Url = "https://youtu.be/bbbbbbbbbbb", Provider = RemedialTrackVideoProvider.YouTube, ExternalId = "bbbbbbbbbbb" },
                    new RemedialTrackVideo { AxisId = axis2.Id, Order = 1, Title = "فيديو منصة أخرى", Url = "https://example.com/v/1", Provider = RemedialTrackVideoProvider.Other, DurationSeconds = 60 });

                var pub = new RemedialTrackPublication
                {
                    TrackId = track.Id, BatchId = 1, Scope = RemedialTrackPublicationScope.WholeBatch, Mode = mode,
                    PublishAtUtc = publishAt ?? T0.AddHours(-1), Status = pubStatus,
                    AccessCode = mode == RemedialTrackDeliveryMode.InPerson ? "123456" : null,
                    CodeVersion = 1, CreatedByUserId = "admin", CreatedAtUtc = T0.AddDays(-2), TotalStudents = 2
                };
                db.RemedialTrackPublications.Add(pub);
                await db.SaveChangesAsync();
                PublicationId = pub.Id;

                EnrollmentA = await AddEnrollmentAsync(db, pub.Id, track.Id, StudentA, axis1.Id, axis2.Id, true);
                EnrollmentB = await AddEnrollmentAsync(db, pub.Id, track.Id, StudentB, axis1.Id, axis2.Id, false);

                Axis1ProgressA = await db.RemedialTrackAxisProgresses.Where(a => a.EnrollmentId == EnrollmentA && a.Order == 1).Select(a => a.Id).SingleAsync();
                Axis2ProgressA = await db.RemedialTrackAxisProgresses.Where(a => a.EnrollmentId == EnrollmentA && a.Order == 2).Select(a => a.Id).SingleAsync();
            }

            private static async Task<int> AddEnrollmentAsync(ApplicationDbContext db, int pubId, int trackId, int studentId, int axis1, int axis2, bool _)
            {
                var e = new RemedialTrackEnrollment { PublicationId = pubId, TrackId = trackId, StudentId = studentId, CreatedAtUtc = T0.AddDays(-2) };
                e.AxisProgresses.Add(new RemedialTrackAxisProgress { AxisId = axis1, Order = 1, Status = RemedialTrackAxisStatus.Videos, Round = 1 });
                e.AxisProgresses.Add(new RemedialTrackAxisProgress { AxisId = axis2, Order = 2, Status = RemedialTrackAxisStatus.Locked, Round = 1 });
                db.RemedialTrackEnrollments.Add(e);
                await db.SaveChangesAsync();
                return e.Id;
            }

            public async Task<int[]> VideoProgressIdsAsync(int axisProgressId, int studentId, int enrollmentId)
            {
                var vm = await Progress.GetAxisAsync(studentId, enrollmentId, axisProgressId);
                return vm!.Videos.Select(v => v.VideoProgressId).ToArray();
            }

            public Task<RemedialTrackPingResult> PingAsync(int vpId, string state, double duration = 0, int? student = null, int? enrollment = null)
                => Progress.RecordPingAsync(student ?? StudentA, new RemedialTrackVideoPingRequest
                {
                    EnrollmentId = enrollment ?? EnrollmentA, VideoProgressId = vpId, State = state, Duration = duration
                });
        }

        private static async Task<Fixture> NewAsync(
            RemedialTrackDeliveryMode mode = RemedialTrackDeliveryMode.Online,
            DateTime? publishAt = null,
            RemedialTrackPublicationStatus status = RemedialTrackPublicationStatus.Active)
        {
            var f = new Fixture();
            await f.SeedAsync(mode, publishAt, status);
            return f;
        }

        // ═══════════ بوابة الوصول (دالة نقية) ═══════════

        private static RemedialTrackAccessSnapshot Snap(
            int student = 1,
            RemedialTrackEnrollmentStatus es = RemedialTrackEnrollmentStatus.InProgress,
            RemedialTrackPublicationStatus ps = RemedialTrackPublicationStatus.Active,
            DateTime? publishAt = null,
            RemedialTrackDeliveryMode mode = RemedialTrackDeliveryMode.Online,
            int codeVersion = 1, int? verified = null, DateTime? lockedUntil = null)
            => new(student, es, ps, publishAt ?? T0.AddHours(-1), mode, codeVersion, verified, lockedUntil);

        [Fact]
        public void Decide_Allowed_ForOnlineActivePublished() =>
            Assert.Equal(RemedialTrackAccessOutcome.Allowed, RemedialTrackAccessService.Decide(Snap(), 1, T0));

        [Fact]
        public void Decide_NotFound_ForOtherStudent() =>
            Assert.Equal(RemedialTrackAccessOutcome.NotFound, RemedialTrackAccessService.Decide(Snap(student: 1), 2, T0));

        [Fact]
        public void Decide_NotYetPublished_WhenPublishAtInFuture() =>
            Assert.Equal(RemedialTrackAccessOutcome.NotYetPublished,
                RemedialTrackAccessService.Decide(Snap(publishAt: T0.AddMinutes(1)), 1, T0));

        [Theory]
        [InlineData(RemedialTrackPublicationStatus.Cancelled)]
        [InlineData(RemedialTrackPublicationStatus.Closed)]
        public void Decide_Cancelled_WhenPublicationNotActive(RemedialTrackPublicationStatus ps) =>
            Assert.Equal(RemedialTrackAccessOutcome.Cancelled, RemedialTrackAccessService.Decide(Snap(ps: ps), 1, T0));

        [Fact]
        public void Decide_Cancelled_WhenEnrollmentCancelled() =>
            Assert.Equal(RemedialTrackAccessOutcome.Cancelled,
                RemedialTrackAccessService.Decide(Snap(es: RemedialTrackEnrollmentStatus.Cancelled), 1, T0));

        [Fact]
        public void Decide_NeedsCode_InPersonWithoutVerification() =>
            Assert.Equal(RemedialTrackAccessOutcome.NeedsCode,
                RemedialTrackAccessService.Decide(Snap(mode: RemedialTrackDeliveryMode.InPerson), 1, T0));

        [Fact]
        public void Decide_NeedsCode_WhenVerifiedWithOldVersion() =>
            Assert.Equal(RemedialTrackAccessOutcome.NeedsCode,
                RemedialTrackAccessService.Decide(Snap(mode: RemedialTrackDeliveryMode.InPerson, codeVersion: 2, verified: 1), 1, T0));

        [Fact]
        public void Decide_Allowed_InPersonVerifiedWithCurrentVersion() =>
            Assert.Equal(RemedialTrackAccessOutcome.Allowed,
                RemedialTrackAccessService.Decide(Snap(mode: RemedialTrackDeliveryMode.InPerson, codeVersion: 2, verified: 2), 1, T0));

        [Fact]
        public void Decide_CodeLocked_WhileLockActive_ThenNeedsCodeAfterExpiry()
        {
            var s = Snap(mode: RemedialTrackDeliveryMode.InPerson, lockedUntil: T0.AddMinutes(10));
            Assert.Equal(RemedialTrackAccessOutcome.CodeLocked, RemedialTrackAccessService.Decide(s, 1, T0));
            Assert.Equal(RemedialTrackAccessOutcome.NeedsCode, RemedialTrackAccessService.Decide(s, 1, T0.AddMinutes(11)));
        }

        [Theory]
        [InlineData("123456", "123456")]
        [InlineData("  123456  ", "123456")]
        [InlineData("١٢٣٤٥٦", "123456")]       // أرقام هندية
        [InlineData("۱۲۳۴۵۶", "123456")]       // أرقام فارسية
        [InlineData("12345", null)]
        [InlineData("1234567", null)]
        [InlineData("12a456", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void NormalizeCode_ConvertsDigitsAndRejectsMalformed(string? raw, string? expected) =>
            Assert.Equal(expected, RemedialTrackAccessService.NormalizeCode(raw));

        // ═══════════ Evaluate / VerifyCode على الخدمة ═══════════

        [Fact]
        public async Task Evaluate_OtherStudentsEnrollment_IsNotFound()
        {
            var f = await NewAsync();
            var r = await f.Access.EvaluateAsync(f.StudentB, f.EnrollmentA);
            Assert.Equal(RemedialTrackAccessOutcome.NotFound, r.Outcome);
        }

        [Fact]
        public async Task Evaluate_Online_Allowed_AndMarksEnrollmentStartedOnce()
        {
            var f = await NewAsync();

            Assert.True(await f.Access.AcceptTermsAsync(f.StudentA, f.EnrollmentA));
            var r = await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA);
            Assert.True(r.IsAllowed);

            f.Clock.Now = T0.AddHours(1);
            await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA);

            await using var db = f.Factory.CreateDbContext();
            var e = await db.RemedialTrackEnrollments.SingleAsync(x => x.Id == f.EnrollmentA);
            Assert.Equal(RemedialTrackEnrollmentStatus.InProgress, e.Status);
            Assert.Equal(T0, e.StartedAtUtc);   // أول وصول فقط
        }

        [Fact]
        public async Task Evaluate_Online_NeedsTerms_UntilAccepted_AndNotStarted()
        {
            var f = await NewAsync();

            Assert.Equal(RemedialTrackAccessOutcome.NeedsTerms, (await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA)).Outcome);
            await using (var db = f.Factory.CreateDbContext())
                Assert.Null((await db.RemedialTrackEnrollments.SingleAsync(x => x.Id == f.EnrollmentA)).StartedAtUtc);

            Assert.True(await f.Access.AcceptTermsAsync(f.StudentA, f.EnrollmentA));
            Assert.True(await f.Access.AcceptTermsAsync(f.StudentA, f.EnrollmentA));   // idempotent
            Assert.True((await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA)).IsAllowed);

            await using var db2 = f.Factory.CreateDbContext();
            Assert.Equal(1, await db2.RemedialTrackEvents.CountAsync(e => e.Type == RemedialTrackEventType.TermsAccepted));
        }

        [Fact]
        public async Task AcceptTerms_OtherStudentsEnrollment_ReturnsFalse()
        {
            var f = await NewAsync();
            Assert.False(await f.Access.AcceptTermsAsync(f.StudentB, f.EnrollmentA));
        }

        [Fact]
        public async Task Evaluate_FuturePublication_NotYetPublished_AndNotStarted()
        {
            var f = await NewAsync(publishAt: T0.AddDays(1));
            var r = await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA);
            Assert.Equal(RemedialTrackAccessOutcome.NotYetPublished, r.Outcome);

            await using var db = f.Factory.CreateDbContext();
            Assert.Null((await db.RemedialTrackEnrollments.SingleAsync(x => x.Id == f.EnrollmentA)).StartedAtUtc);
        }

        [Fact]
        public async Task Evaluate_CancelledPublication_IsCancelled()
        {
            var f = await NewAsync(status: RemedialTrackPublicationStatus.Cancelled);
            Assert.Equal(RemedialTrackAccessOutcome.Cancelled, (await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA)).Outcome);
        }

        [Fact]
        public async Task VerifyCode_Success_StoresVersionInDb_AndOpensGate()
        {
            var f = await NewAsync(RemedialTrackDeliveryMode.InPerson);
            Assert.Equal(RemedialTrackAccessOutcome.NeedsCode, (await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA)).Outcome);

            var v = await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, "123456");

            Assert.True(v.IsVerified);
            Assert.Equal(RemedialTrackAccessOutcome.NeedsTerms, (await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA)).Outcome);
            Assert.True(await f.Access.AcceptTermsAsync(f.StudentA, f.EnrollmentA));
            Assert.True((await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA)).IsAllowed);
            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(1, (await db.RemedialTrackEnrollments.SingleAsync(x => x.Id == f.EnrollmentA)).VerifiedCodeVersion);
            Assert.Equal(1, await db.RemedialTrackEvents.CountAsync(e => e.Type == RemedialTrackEventType.CodeVerified));
        }

        [Fact]
        public async Task VerifyCode_AcceptsIndicDigits()
        {
            var f = await NewAsync(RemedialTrackDeliveryMode.InPerson);
            Assert.True((await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, "١٢٣٤٥٦")).IsVerified);
        }

        [Fact]
        public async Task VerifyCode_WrongCode_IsInvalid_AndCounts()
        {
            var f = await NewAsync(RemedialTrackDeliveryMode.InPerson);
            var v = await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, "000000");

            Assert.Equal(RemedialTrackVerifyOutcome.Invalid, v.Outcome);
            await using var db = f.Factory.CreateDbContext();
            var e = await db.RemedialTrackEnrollments.SingleAsync(x => x.Id == f.EnrollmentA);
            Assert.Equal(1, e.FailedCodeAttempts);
            Assert.Null(e.VerifiedCodeVersion);
            Assert.Equal(1, await db.RemedialTrackEvents.CountAsync(x => x.Type == RemedialTrackEventType.CodeFailed));
        }

        [Fact]
        public async Task VerifyCode_MalformedCode_IsInvalid_WithoutCounting()
        {
            var f = await NewAsync(RemedialTrackDeliveryMode.InPerson);
            Assert.Equal(RemedialTrackVerifyOutcome.Invalid, (await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, "12")).Outcome);

            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(0, (await db.RemedialTrackEnrollments.SingleAsync(x => x.Id == f.EnrollmentA)).FailedCodeAttempts);
        }

        [Fact]
        public async Task VerifyCode_FiveFailures_LocksFor15Minutes_AndCorrectCodeStillRejectedWhileLocked()
        {
            var f = await NewAsync(RemedialTrackDeliveryMode.InPerson);

            for (var i = 0; i < 4; i++)
                Assert.Equal(RemedialTrackVerifyOutcome.Invalid, (await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, "000000")).Outcome);

            var fifth = await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, "000000");
            Assert.Equal(RemedialTrackVerifyOutcome.Locked, fifth.Outcome);
            Assert.Equal(T0.AddMinutes(15), fifth.LockedUntilUtc);

            // الرقم الصحيح أثناء القفل لا يُقبل ولا تُنفَّذ المقارنة
            Assert.Equal(RemedialTrackVerifyOutcome.Locked, (await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, "123456")).Outcome);
            Assert.Equal(RemedialTrackAccessOutcome.CodeLocked, (await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA)).Outcome);

            // بعد انتهاء القفل يعمل الرقم الصحيح، والعدّاد صُفّر
            f.Clock.Now = T0.AddMinutes(16);
            Assert.True((await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, "123456")).IsVerified);
            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(0, (await db.RemedialTrackEnrollments.SingleAsync(x => x.Id == f.EnrollmentA)).FailedCodeAttempts);
        }

        [Fact]
        public async Task VerifyCode_AfterCodeRenewal_NeedsCodeAgain()
        {
            var f = await NewAsync(RemedialTrackDeliveryMode.InPerson);
            Assert.True((await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, "123456")).IsVerified);
            Assert.True(await f.Access.AcceptTermsAsync(f.StudentA, f.EnrollmentA));

            await using (var db = f.Factory.CreateDbContext())
            {
                var pub = await db.RemedialTrackPublications.SingleAsync();
                pub.AccessCode = "654321";   // تجديد يدوي (D7)
                pub.CodeVersion = 2;
                await db.SaveChangesAsync();
            }

            Assert.Equal(RemedialTrackAccessOutcome.NeedsCode, (await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA)).Outcome);
            Assert.Equal(RemedialTrackVerifyOutcome.Invalid, (await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, "123456")).Outcome);
            Assert.True((await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, "654321")).IsVerified);
            Assert.True((await f.Access.EvaluateAsync(f.StudentA, f.EnrollmentA)).IsAllowed);
        }

        [Fact]
        public async Task VerifyCode_OtherStudentsEnrollment_IsNotFound()
        {
            var f = await NewAsync(RemedialTrackDeliveryMode.InPerson);
            Assert.Equal(RemedialTrackVerifyOutcome.NotFound, (await f.Access.VerifyCodeAsync(f.StudentB, f.EnrollmentA, "123456")).Outcome);
        }

        [Fact]
        public async Task VerifyCode_Online_NeedsNoCode()
        {
            var f = await NewAsync();
            Assert.True((await f.Access.VerifyCodeAsync(f.StudentA, f.EnrollmentA, null)).IsVerified);
        }

        [Fact]
        public async Task HasVisibleEnrollment_TrueOnlyForPublishedActive()
        {
            var visible = await NewAsync();
            Assert.True(await visible.Access.HasVisibleEnrollmentAsync(visible.StudentA));
            Assert.False(await visible.Access.HasVisibleEnrollmentAsync(999));

            var future = await NewAsync(publishAt: T0.AddDays(1));
            Assert.False(await future.Access.HasVisibleEnrollmentAsync(future.StudentA));

            var cancelled = await NewAsync(status: RemedialTrackPublicationStatus.Cancelled);
            Assert.False(await cancelled.Access.HasVisibleEnrollmentAsync(cancelled.StudentA));
        }

        // ═══════════ قائمة الطالب ═══════════

        [Fact]
        public async Task Index_ShowsPublishedPlan_WithModeAndProgress()
        {
            var f = await NewAsync(RemedialTrackDeliveryMode.InPerson);
            await using (var db = f.Factory.CreateDbContext())
            {
                var ap = await db.RemedialTrackAxisProgresses.FirstAsync(a => a.Id == f.Axis1ProgressA);
                ap.Status = RemedialTrackAxisStatus.Passed;
                await db.SaveChangesAsync();
            }

            var vm = await f.Progress.GetMyPlansAsync(f.StudentA);

            var item = Assert.Single(vm.Items);
            Assert.Equal("خطة علاجية", item.TrackTitle);
            Assert.Equal(RemedialTrackDeliveryMode.InPerson, item.Mode);
            Assert.Equal(T0.AddHours(-1).AddHours(3), item.PublishAtLocal);
            Assert.Equal(1, item.PassedAxes);
            Assert.Equal(2, item.TotalAxes);
            Assert.Equal(50, item.ProgressPercent);
        }

        [Fact]
        public async Task Index_HidesFuture_Cancelled_AndOtherStudents()
        {
            var future = await NewAsync(publishAt: T0.AddMinutes(5));
            Assert.Empty((await future.Progress.GetMyPlansAsync(future.StudentA)).Items);

            var cancelled = await NewAsync(status: RemedialTrackPublicationStatus.Cancelled);
            Assert.Empty((await cancelled.Progress.GetMyPlansAsync(cancelled.StudentA)).Items);

            var f = await NewAsync();
            Assert.Empty((await f.Progress.GetMyPlansAsync(777)).Items);

            await using (var db = f.Factory.CreateDbContext())
            {
                (await db.RemedialTrackEnrollments.SingleAsync(e => e.Id == f.EnrollmentA)).Status = RemedialTrackEnrollmentStatus.Cancelled;
                await db.SaveChangesAsync();
            }
            Assert.Empty((await f.Progress.GetMyPlansAsync(f.StudentA)).Items);
            Assert.Single((await f.Progress.GetMyPlansAsync(f.StudentB)).Items);
        }

        // ═══════════ صفحتا الخطة والمحور ═══════════

        [Fact]
        public async Task GetPlan_ReturnsAxesInOrder_AndHidesFromOtherStudent()
        {
            var f = await NewAsync();
            var plan = await f.Progress.GetPlanAsync(f.StudentA, f.EnrollmentA);

            Assert.NotNull(plan);
            Assert.Equal(new[] { 1, 2 }, plan!.Axes.Select(a => a.Order));
            Assert.True(plan.Axes[0].IsCurrent);
            Assert.True(plan.Axes[1].IsLocked);
            Assert.Equal(2, plan.Axes[0].VideoCount);

            Assert.Null(await f.Progress.GetPlanAsync(f.StudentB, f.EnrollmentA));
        }

        [Fact]
        public async Task GetAxis_CreatesVideoRowsLazily_Once_AndUnlocksOnlyFirst()
        {
            var f = await NewAsync();

            var vm = await f.Progress.GetAxisAsync(f.StudentA, f.EnrollmentA, f.Axis1ProgressA);
            var again = await f.Progress.GetAxisAsync(f.StudentA, f.EnrollmentA, f.Axis1ProgressA);

            Assert.NotNull(vm);
            Assert.True(vm!.CanWatch);
            Assert.Equal(2, vm.Videos.Count);
            Assert.True(vm.Videos[0].IsUnlocked);
            Assert.False(vm.Videos[1].IsUnlocked);
            Assert.Equal("https://www.youtube-nocookie.com/embed/aaaaaaaaaaa", vm.Videos[0].EmbedUrl);
            Assert.Equal(90, vm.Videos[0].RequiredSeconds);
            Assert.Equal(vm.Videos.Select(v => v.VideoProgressId), again!.Videos.Select(v => v.VideoProgressId));

            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(2, await db.RemedialTrackVideoProgresses.CountAsync());
        }

        [Fact]
        public async Task GetAxis_LockedAxis_OtherStudent_AndMismatchedEnrollment_AreNull()
        {
            var f = await NewAsync();
            Assert.Null(await f.Progress.GetAxisAsync(f.StudentA, f.EnrollmentA, f.Axis2ProgressA));       // مغلق
            Assert.Null(await f.Progress.GetAxisAsync(f.StudentB, f.EnrollmentA, f.Axis1ProgressA));       // طالب آخر
            Assert.Null(await f.Progress.GetAxisAsync(f.StudentA, f.EnrollmentB, f.Axis1ProgressA));       // تسجيل لا يخص المحور
        }

        // ═══════════ حساب النبضة (نقي) ═══════════

        [Fact]
        public void ComputePing_FirstPing_GivesNoCredit()
        {
            var c = RemedialTrackProgressService.ComputePing(0, false, null, null, T0, "playing",
                RemedialTrackVideoProvider.YouTube, 100, null, 0, 90);
            Assert.Equal(0, c.WatchedSeconds);
            Assert.False(c.Completed);
        }

        [Fact]
        public void ComputePing_CreditOnlyWhenPreviousStateWasPlaying_CappedAt20()
        {
            var paused = RemedialTrackProgressService.ComputePing(10, false, "paused", T0, T0.AddSeconds(15), "playing",
                RemedialTrackVideoProvider.YouTube, 100, null, 0, 90);
            Assert.Equal(10, paused.WatchedSeconds);

            var capped = RemedialTrackProgressService.ComputePing(10, false, "playing", T0, T0.AddSeconds(300), "playing",
                RemedialTrackVideoProvider.YouTube, 100, null, 0, 90);
            Assert.Equal(30, capped.WatchedSeconds);
        }

        [Fact]
        public void ComputePing_AdminDurationOverridesClient_AndClientDurationNeverDecreases()
        {
            var admin = RemedialTrackProgressService.ComputePing(0, false, null, null, T0, "playing",
                RemedialTrackVideoProvider.YouTube, 200, null, 5, 90);
            Assert.Equal(200, admin.DurationSeconds);

            var kept = RemedialTrackProgressService.ComputePing(0, false, null, null, T0, "playing",
                RemedialTrackVideoProvider.YouTube, null, 600, 5, 90);
            Assert.Equal(600, kept.DurationSeconds);

            var nan = RemedialTrackProgressService.ComputePing(0, false, null, null, T0, "playing",
                RemedialTrackVideoProvider.YouTube, null, null, double.NaN, 90);
            Assert.Equal(5, nan.DurationSeconds);
        }

        [Fact]
        public void ComputePing_EndedBeforeRequired_IsNotCompleted()
        {
            var c = RemedialTrackProgressService.ComputePing(40, false, "playing", T0, T0.AddSeconds(10), "ended",
                RemedialTrackVideoProvider.YouTube, 100, null, 0, 90);
            Assert.True(c.Ended);
            Assert.False(c.Completed);
            Assert.Equal(90, c.RequiredSeconds);
        }

        // ═══════════ RecordPingAsync ═══════════

        [Fact]
        public async Task Ping_WatchingRealTime_CompletesVideo_UnlocksNext_FromServer()
        {
            var f = await NewAsync();
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);

            for (var s = 0; s <= 90; s += 15)
            {
                f.Clock.Now = T0.AddSeconds(s);
                var mid = await f.PingAsync(ids[0], "playing");
                Assert.Equal(RemedialTrackPingStatus.Ok, mid.Status);
                Assert.False(mid.Body.Completed);
            }

            var end = await f.PingAsync(ids[0], "ended");

            Assert.Equal(RemedialTrackPingStatus.Ok, end.Status);
            Assert.True(end.Body.Completed);
            Assert.False(end.Body.AllDone);
            Assert.NotNull(end.Body.NextVideo);
            Assert.Equal(ids[1], end.Body.NextVideo!.VideoProgressId);

            await using var db = f.Factory.CreateDbContext();
            var vp = await db.RemedialTrackVideoProgresses.SingleAsync(v => v.Id == ids[0]);
            Assert.True(vp.IsCompleted);
            Assert.NotNull(vp.CompletedAtUtc);
            Assert.Equal(1, await db.RemedialTrackEvents.CountAsync(e => e.Type == RemedialTrackEventType.VideoCompleted));
            // الفيديو الثاني ما زال غير مكتمل فالمحور لم ينتقل
            Assert.Equal(RemedialTrackAxisStatus.Videos, (await db.RemedialTrackAxisProgresses.SingleAsync(a => a.Id == f.Axis1ProgressA)).Status);
        }

        [Fact]
        public async Task Ping_SeekToEnd_DoesNotComplete_Insufficient()
        {
            var f = await NewAsync();
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);

            await f.PingAsync(ids[0], "playing");
            f.Clock.Now = T0.AddSeconds(2);
            var end = await f.PingAsync(ids[0], "ended");

            Assert.Equal(RemedialTrackPingStatus.Ok, end.Status);
            Assert.False(end.Body.Completed);
            Assert.Equal("insufficient", end.Body.Reason);
            Assert.Null(end.Body.NextVideo);
            Assert.True(end.Body.WatchedSeconds < end.Body.RequiredSeconds);
        }

        [Fact]
        public async Task Ping_PausedTimeGivesNoCredit()
        {
            var f = await NewAsync();
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);

            await f.PingAsync(ids[0], "playing");
            f.Clock.Now = T0.AddSeconds(10);
            await f.PingAsync(ids[0], "paused");               // يُحتسب 10ث (الفترة السابقة كانت playing)
            f.Clock.Now = T0.AddSeconds(500);
            var r = await f.PingAsync(ids[0], "playing");      // الفترة السابقة paused → لا رصيد

            Assert.Equal(10, r.Body.WatchedSeconds);
        }

        [Fact]
        public async Task Ping_OutOfSequence_IsConflict()
        {
            var f = await NewAsync();
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);

            var r = await f.PingAsync(ids[1], "playing", duration: 50);

            Assert.Equal(RemedialTrackPingStatus.Conflict, r.Status);
            Assert.Equal("locked", r.Body.Reason);
        }

        [Fact]
        public async Task Ping_OtherStudentsVideo_IsNotFound()
        {
            var f = await NewAsync();
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);

            var asB = await f.PingAsync(ids[0], "playing", student: f.StudentB, enrollment: f.EnrollmentA);
            var asBOwnEnrollment = await f.PingAsync(ids[0], "playing", student: f.StudentB, enrollment: f.EnrollmentB);

            Assert.Equal(RemedialTrackPingStatus.NotFound, asB.Status);
            Assert.Equal(RemedialTrackPingStatus.NotFound, asBOwnEnrollment.Status);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("seeking")]
        public async Task Ping_InvalidState_IsBadRequest(string? state)
        {
            var f = await NewAsync();
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);
            Assert.Equal(RemedialTrackPingStatus.BadRequest, (await f.PingAsync(ids[0], state!)).Status);
        }

        [Fact]
        public async Task Ping_ManualDone_RejectedForYouTube()
        {
            var f = await NewAsync();
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);
            Assert.Equal(RemedialTrackPingStatus.BadRequest, (await f.PingAsync(ids[0], "manual-done")).Status);
        }

        [Fact]
        public async Task Ping_InPersonWithoutVerifiedCode_NeedsCode()
        {
            var f = await NewAsync(RemedialTrackDeliveryMode.InPerson);
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);

            var r = await f.PingAsync(ids[0], "playing");

            Assert.Equal(RemedialTrackPingStatus.NeedsCode, r.Status);
            Assert.True(r.Body.NeedsCode);
        }

        [Fact]
        public async Task Ping_CancelledOrFuturePublication_IsForbidden()
        {
            var f = await NewAsync();
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);
            await using (var db = f.Factory.CreateDbContext())
            {
                (await db.RemedialTrackPublications.SingleAsync()).Status = RemedialTrackPublicationStatus.Cancelled;
                await db.SaveChangesAsync();
            }
            Assert.Equal(RemedialTrackPingStatus.Forbidden, (await f.PingAsync(ids[0], "playing")).Status);
        }

        [Fact]
        public async Task Ping_AxisNotInVideosState_IsConflict()
        {
            var f = await NewAsync();
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);
            await using (var db = f.Factory.CreateDbContext())
            {
                (await db.RemedialTrackAxisProgresses.SingleAsync(a => a.Id == f.Axis1ProgressA)).Status = RemedialTrackAxisStatus.AwaitingExam101;
                await db.SaveChangesAsync();
            }
            Assert.Equal(RemedialTrackPingStatus.Conflict, (await f.PingAsync(ids[0], "playing")).Status);
        }

        [Fact]
        public async Task Ping_CompletingLastVideo_MovesAxisToAwaitingExam101_AndReportsAllDone()
        {
            var f = await NewAsync();
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);

            // الفيديو 1 (مدة الأدمن 100ث)
            for (var s = 0; s <= 90; s += 15) { f.Clock.Now = T0.AddSeconds(s); await f.PingAsync(ids[0], "playing"); }
            Assert.True((await f.PingAsync(ids[0], "ended")).Body.Completed);

            // الفيديو 2 (بلا مدة من الأدمن ← مدة العميل 40ث → المطلوب 36ث)
            var t = 100;
            f.Clock.Now = T0.AddSeconds(t);
            await f.PingAsync(ids[1], "playing", duration: 40);
            for (var i = 0; i < 3; i++) { t += 15; f.Clock.Now = T0.AddSeconds(t); await f.PingAsync(ids[1], "playing", duration: 40); }
            t += 0;
            var last = await f.PingAsync(ids[1], "ended", duration: 40);

            Assert.True(last.Body.Completed);
            Assert.True(last.Body.AllDone);
            Assert.Null(last.Body.NextVideo);

            await using var db = f.Factory.CreateDbContext();
            var ap = await db.RemedialTrackAxisProgresses.SingleAsync(a => a.Id == f.Axis1ProgressA);
            Assert.Equal(RemedialTrackAxisStatus.AwaitingExam101, ap.Status);
            Assert.Equal(1, ap.Round);
            Assert.Equal(2, await db.RemedialTrackEvents.CountAsync(e => e.Type == RemedialTrackEventType.VideoCompleted));

            // بعد الانتقال لا تُقبل نبضات جديدة
            Assert.Equal(RemedialTrackPingStatus.Conflict, (await f.PingAsync(ids[1], "playing", duration: 40)).Status);
        }

        [Fact]
        public async Task Ping_AlreadyCompletedVideo_IsIdempotent_AndDoesNotDuplicateEvent()
        {
            var f = await NewAsync();
            var ids = await f.VideoProgressIdsAsync(f.Axis1ProgressA, f.StudentA, f.EnrollmentA);
            for (var s = 0; s <= 90; s += 15) { f.Clock.Now = T0.AddSeconds(s); await f.PingAsync(ids[0], "playing"); }
            await f.PingAsync(ids[0], "ended");

            var again = await f.PingAsync(ids[0], "ended");

            Assert.True(again.Body.Completed);
            Assert.Equal(ids[1], again.Body.NextVideo!.VideoProgressId);
            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(1, await db.RemedialTrackEvents.CountAsync(e => e.Type == RemedialTrackEventType.VideoCompleted));
        }

        [Fact]
        public async Task Ping_OtherProvider_ManualDone_RequiresServerWatchTime()
        {
            var f = await NewAsync();
            // اجعل المحور الثاني مفتوحًا (جولة 1) ليُختبر فيديو «منصة أخرى» (مدة 60ث → المطلوب 54ث)
            await using (var db = f.Factory.CreateDbContext())
            {
                (await db.RemedialTrackAxisProgresses.SingleAsync(a => a.Id == f.Axis2ProgressA)).Status = RemedialTrackAxisStatus.Videos;
                await db.SaveChangesAsync();
            }
            var ids = await f.VideoProgressIdsAsync(f.Axis2ProgressA, f.StudentA, f.EnrollmentA);

            await f.PingAsync(ids[0], "playing");
            f.Clock.Now = T0.AddSeconds(5);
            var early = await f.PingAsync(ids[0], "manual-done");
            Assert.False(early.Body.Completed);
            Assert.Equal("insufficient", early.Body.Reason);

            // بعد manual-done المبكر آخر حالة «ended» فلا رصيد عن الفترة التالية؛ يستأنف الرصيد من النبضة التالية
            for (var s = 20; s <= 65; s += 15) { f.Clock.Now = T0.AddSeconds(s); await f.PingAsync(ids[0], "playing"); }
            f.Clock.Now = T0.AddSeconds(70);   // 5 (قبل) + 45 + 5 = 55 ≥ 54
            var done = await f.PingAsync(ids[0], "manual-done");

            Assert.True(done.Body.Completed);
            Assert.True(done.Body.AllDone);
        }
    }
}
