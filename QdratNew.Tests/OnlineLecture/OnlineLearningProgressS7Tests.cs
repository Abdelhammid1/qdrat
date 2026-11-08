using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Areas.Students.Controllers;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Services.OnlineLecture;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.OnlineLecture;
using Xunit;

namespace QdratNew.Tests.OnlineLecture
{
    /// <summary>وقت قابل للتحكم للاختبارات (P5: الرصيد بزمن الخادم).</summary>
    internal sealed class OlcTestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(double seconds) => Now = Now.AddSeconds(seconds);
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S7.1 — LectureProgressCalculator (دالة نقية — R9 + C.7-P4/P5)
    // ═════════════════════════════════════════════════════════════════════
    public class LectureProgressCalculatorTests
    {
        private static readonly DateTime T0 = new(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void FirstPing_GivesNoCredit()
        {
            var c = LectureProgressCalculator.Compute(0, false, null, null, T0, "playing", 600, null, 600, 0, 0);
            Assert.Equal(0, c.WatchedSeconds);
            Assert.Equal(480, c.RequiredSeconds);   // ceil(600 × 80%)
            Assert.False(c.Completed);
        }

        [Fact]
        public void Credit_OnlyAfterPlaying_AndCappedAt20Seconds()
        {
            var afterPlaying = LectureProgressCalculator.Compute(100, false, "playing", T0, T0.AddSeconds(60), "playing", 600, null, 600, 115, 100);
            Assert.Equal(120, afterPlaying.WatchedSeconds);   // 60ث منقضية لكن الرصيد ≤ 20 (موضع العميل لم يتقدّم إلا 15ث)

            var afterPaused = LectureProgressCalculator.Compute(100, false, "paused", T0, T0.AddSeconds(15), "playing", 600, null, 600, 115, 100);
            Assert.Equal(100, afterPaused.WatchedSeconds);    // لا رصيد عن فترة الإيقاف
        }

        [Fact]
        public void P4_FakeForwardPosition_IsClampedToElapsedPlusTolerance()
        {
            var c = LectureProgressCalculator.Compute(15, false, "playing", T0, T0.AddSeconds(15), "playing", 600, null, 600, clientPosition: 590, previousMaxPosition: 15);
            Assert.Equal(15 + 15 + LectureWatchRules.PositionToleranceSeconds, c.MaxPositionSeconds);
        }

        [Fact]
        public void GapOver20Seconds_WithProvenPositionProgress_IsCreditedUpTo600()
        {
            // انقطاع إنترنت 120ث والموضع تقدّم 110ث فعلًا ⇒ يُحتسب 110 لا 20
            var c = LectureProgressCalculator.Compute(100, false, "playing", T0, T0.AddSeconds(120), "playing", 3600, null, 3600, 210, 100);
            Assert.Equal(210, c.WatchedSeconds);

            // فجوة ضخمة: لا تتجاوز 600
            var big = LectureProgressCalculator.Compute(0, false, "playing", T0, T0.AddSeconds(5000), "playing", 7200, null, 7200, 5000, 0);
            Assert.Equal(LectureWatchRules.MaxGapCreditSeconds, big.WatchedSeconds);
        }

        [Fact]
        public void Completion_RequiresEnded_And80Percent()
        {
            var endedEarly = LectureProgressCalculator.Compute(100, false, "playing", T0, T0.AddSeconds(15), "ended", 600, null, 600, 600, 100);
            Assert.True(endedEarly.Ended);
            Assert.False(endedEarly.Completed);

            var notEnded = LectureProgressCalculator.Compute(500, false, "playing", T0, T0.AddSeconds(15), "playing", 600, null, 600, 515, 500);
            Assert.False(notEnded.Completed);

            var done = LectureProgressCalculator.Compute(470, false, "playing", T0, T0.AddSeconds(15), "ended", 600, null, 600, 600, 590);
            Assert.True(done.Completed);

            // ended سابق يبقى: الإتمام عند بلوغ 80% لاحقًا
            var later = LectureProgressCalculator.Compute(470, true, "playing", T0, T0.AddSeconds(15), "playing", 600, null, 600, 100, 590);
            Assert.True(later.Completed);
        }

        [Fact]
        public void Duration_AdminWins_StoredNeverDecreases_ClientClampedAndSafe()
        {
            Assert.Equal(600, LectureProgressCalculator.Compute(0, false, null, null, T0, "playing", 600, 900, 50, 0, 0).DurationSeconds);
            Assert.Equal(900, LectureProgressCalculator.Compute(0, false, null, null, T0, "playing", null, 900, 50, 0, 0).DurationSeconds);
            Assert.Equal(LectureProgressCalculator.MinClientDurationSeconds,
                LectureProgressCalculator.Compute(0, false, null, null, T0, "playing", null, null, 1, 0, 0).DurationSeconds);
            Assert.Equal(LectureProgressCalculator.MaxClientDurationSeconds,
                LectureProgressCalculator.Compute(0, false, null, null, T0, "playing", null, null, double.MaxValue, 0, 0).DurationSeconds);

            var nan = LectureProgressCalculator.Compute(0, false, "playing", T0, T0.AddSeconds(10), "playing", null, null, double.NaN, double.NaN, double.NaN);
            Assert.False(double.IsNaN(nan.WatchedSeconds) || double.IsNaN(nan.MaxPositionSeconds));
        }

        // حارس النقل: نفس نتائج ComputePing في الخطة العلاجية لمزوّد YouTube وحد 80% (C.7 «يُطبَّق كما هو»)
        [Theory]
        [InlineData(0, false, null, 0, "playing", 600, 0, 0, 0)]
        [InlineData(100, false, "playing", 15, "playing", 600, 600, 115, 100)]
        [InlineData(100, false, "playing", 90, "paused", null, 300, 400, 100)]
        [InlineData(470, false, "playing", 15, "ended", 600, 600, 600, 590)]
        [InlineData(30, true, "paused", 15, "playing", null, 45, 10, 40)]
        [InlineData(0, false, "playing", 700, "playing", 7200, 7200, 650, 0)]
        public void MatchesRemedialTrackComputePing(double watched, bool ended, string? lastState, double elapsed, string state,
            int? admin, double clientDuration, double position, double prevMax)
        {
            var at = lastState is null ? (DateTime?)null : T0;
            var now = T0.AddSeconds(elapsed);
            var olc = LectureProgressCalculator.Compute(watched, ended, lastState, at, now, state, admin, null, clientDuration, position, prevMax);
            var rtk = RemedialTrackProgressService.ComputePing(watched, ended, lastState, at, now, state, RemedialTrackVideoProvider.YouTube,
                admin, null, clientDuration, 80, position, prevMax);

            Assert.Equal(rtk.WatchedSeconds, olc.WatchedSeconds, 6);
            Assert.Equal(rtk.MaxPositionSeconds, olc.MaxPositionSeconds, 6);
            Assert.Equal(rtk.RequiredSeconds, olc.RequiredSeconds, 6);
            Assert.Equal(rtk.Ended, olc.Ended);
            Assert.Equal(rtk.Completed, olc.Completed);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S7.1/S7.2 — IStudentLectureProgressService (P7 كاملة + R13/R14)
    // ═════════════════════════════════════════════════════════════════════
    public class StudentLectureProgressServiceTests
    {
        private static readonly DateTime D1 = new(2026, 11, 1), D2 = new(2026, 11, 2), D3 = new(2026, 11, 3);

        private static StudentLectureProgressService Svc(ApplicationDbContext db, OlcTestClock clock, bool enabled = true)
            => new(db, new OnlineLearningAccessService(db, OlcS5Data.Feature(enabled)), new LectureSequenceService(db), clock,
                NullLogger<StudentLectureProgressService>.Instance);

        private static StudentLecturePingRequest Req(int contentId, string state, double position, double duration = 600, int? itemId = null)
            => new() { LectureContentId = contentId, ItemId = itemId, State = state, Position = position, Duration = duration };

        /// <summary>مشاهدة طبيعية حتى النهاية: نبضة playing كل 15ث بزمن الخادم ثم ended.</summary>
        private static async Task<LecturePingResult> WatchToEndAsync(StudentLectureProgressService svc, OlcTestClock clock, int studentId,
            int contentId, double duration, int? itemId = null)
        {
            var pos = 0.0;
            var r = await svc.RecordPingAsync(studentId, Req(contentId, "playing", pos, duration, itemId));
            while (pos < duration)
            {
                clock.Advance(15);
                pos = Math.Min(duration, pos + 15);
                r = await svc.RecordPingAsync(studentId, Req(contentId, pos >= duration ? "ended" : "playing", pos, duration, itemId));
            }
            return r;
        }

        [Fact]
        public async Task InvalidState_OrShape_IsBadRequest()
        {
            using var db = OlcS5Data.Create();
            var c1 = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var svc = Svc(db, new OlcTestClock());

            Assert.Equal(LecturePingStatus.BadRequest, (await svc.RecordPingAsync(OlcS5Data.Student, Req(c1, "manual-done", 0))).Status);
            Assert.Equal(LecturePingStatus.BadRequest, (await svc.RecordPingAsync(OlcS5Data.Student, Req(c1, "", 0))).Status);
            Assert.Equal(LecturePingStatus.BadRequest, (await svc.RecordPingAsync(OlcS5Data.Student, Req(0, "playing", 0))).Status);
            // FullVideo لا يقبل itemId
            Assert.Equal(LecturePingStatus.BadRequest, (await svc.RecordPingAsync(OlcS5Data.Student, Req(c1, "playing", 0, itemId: 5))).Status);
            Assert.False(await db.LectureContentProgress.AnyAsync());
        }

        [Fact]
        public async Task FeatureOff_OtherBatch_Withdrawn_Draft_AreNotFound_WithoutWrites()
        {
            using var db = OlcS5Data.Create();
            var mine = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var draft = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2, status: LectureContentStatus.Draft);
            var other = OlcS5Data.Content(db, OlcS5Data.PartnerOkBatch, OlcS5Data.Sec1, D1);
            var clock = new OlcTestClock();

            Assert.Equal(LecturePingStatus.NotFound, (await Svc(db, clock, enabled: false).RecordPingAsync(OlcS5Data.Student, Req(mine, "playing", 0))).Status);
            Assert.Equal(LecturePingStatus.NotFound, (await Svc(db, clock).RecordPingAsync(OlcS5Data.Student, Req(draft, "playing", 0))).Status);
            Assert.Equal(LecturePingStatus.NotFound, (await Svc(db, clock).RecordPingAsync(OlcS5Data.Student, Req(other, "playing", 0))).Status);
            Assert.Equal(LecturePingStatus.NotFound, (await Svc(db, clock).RecordPingAsync(OlcS5Data.Withdrawn, Req(mine, "playing", 0))).Status);
            Assert.Equal(LecturePingStatus.NotFound, (await Svc(db, clock).RecordPingAsync(OlcS5Data.Inactive, Req(mine, "playing", 0))).Status);

            var r = await Svc(db, clock).RecordPingAsync(OlcS5Data.Student, Req(other, "playing", 0));
            Assert.Equal("notfound", r.Body.Reason);
            Assert.False(await db.LectureContentProgress.AnyAsync());
        }

        [Fact]
        public async Task R13_LockedLecture_PingRejected_NoRowCreated()
        {
            using var db = OlcS5Data.Create();
            OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var second = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);

            var r = await Svc(db, new OlcTestClock()).RecordPingAsync(OlcS5Data.Student, Req(second, "playing", 0));

            Assert.Equal(LecturePingStatus.Conflict, r.Status);
            Assert.Equal("locked", r.Body.Reason);
            Assert.Equal(StudentLectureProgressService.LockedLectureMessage, r.Body.Message);
            Assert.False(await db.LectureContentProgress.AnyAsync());
        }

        [Fact]
        public async Task FullVideo_WatchToEnd_Completes_AndUnlocksNextLecture_FromServer()
        {
            using var db = OlcS5Data.Create();
            var first = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var second = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);
            var clock = new OlcTestClock();
            var svc = Svc(db, clock);

            var r = await WatchToEndAsync(svc, clock, OlcS5Data.Student, first, 600);

            Assert.Equal(LecturePingStatus.Ok, r.Status);
            Assert.True(r.Body.Ok);
            Assert.True(r.Body.Completed);
            Assert.True(r.Body.LectureCompleted);
            Assert.True(r.Body.NextLectureUnlocked);
            Assert.Null(r.Body.NextItemId);
            Assert.Equal(480, r.Body.RequiredSeconds);
            Assert.True(r.Body.WatchedSeconds >= 480);

            var row = await db.LectureContentProgress.AsNoTracking().SingleAsync(p => p.LectureContentId == first && p.StudentId == OlcS5Data.Student);
            Assert.True(row.IsCompleted);
            Assert.True(row.EndedSeen);
            Assert.NotNull(row.CompletedAtUtc);
            Assert.NotNull(row.FirstPingAtUtc);
            Assert.Equal(600, row.DurationSeconds);

            // التالية صارت مفتوحة على الخادم: نبضتها مقبولة
            var next = await svc.RecordPingAsync(OlcS5Data.Student, Req(second, "playing", 0));
            Assert.Equal(LecturePingStatus.Ok, next.Status);
        }

        [Fact]
        public async Task LastLectureInCurriculum_Completes_WithoutNextUnlocked()
        {
            using var db = OlcS5Data.Create();
            var only = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec2, D2);   // منهج آخر: ليست «التالية»
            var clock = new OlcTestClock();

            var r = await WatchToEndAsync(Svc(db, clock), clock, OlcS5Data.Student, only, 600);
            Assert.True(r.Body.LectureCompleted);
            Assert.False(r.Body.NextLectureUnlocked);
        }

        [Fact]
        public async Task P5_EndedWithoutEnoughServerTime_IsInsufficient_WithRestartMessage()
        {
            using var db = OlcS5Data.Create();
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var clock = new OlcTestClock();
            var svc = Svc(db, clock);

            await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 0));
            clock.Advance(15);
            var r = await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "ended", 600));   // قفز للنهاية بأدوات المطوّر

            Assert.Equal(LecturePingStatus.Ok, r.Status);
            Assert.False(r.Body.Completed);
            Assert.False(r.Body.LectureCompleted);
            Assert.Equal("insufficient", r.Body.Reason);
            Assert.Equal(StudentLectureProgressService.InsufficientMessage, r.Body.Message);
            Assert.Equal(15, r.Body.WatchedSeconds);

            var row = await db.LectureContentProgress.AsNoTracking().SingleAsync();
            Assert.False(row.IsCompleted);
            Assert.Equal(15 + LectureWatchRules.PositionToleranceSeconds, row.MaxPositionSeconds);   // P4: الموضع المزيّف لا يُقبل
        }

        [Fact]
        public async Task S72_Resume_FromServerMaxPosition_InWatchPage()
        {
            using var db = OlcS5Data.Create();
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var clock = new OlcTestClock();
            var svc = Svc(db, clock);

            var pos = 0.0;
            await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", pos));
            for (var i = 0; i < 4; i++)
            {
                clock.Advance(15);
                pos += 15;
                await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", pos));
            }
            clock.Advance(5);
            await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "paused", pos + 5));   // pause/pagehide

            var watch = await new StudentLectureWatchService(db, new OnlineLearningAccessService(db, OlcS5Data.Feature(true)), new LectureSequenceService(db))
                .GetWatchAsync(OlcS5Data.Student, c);
            Assert.Equal(LectureWatchOutcome.Ok, watch.Outcome);
            Assert.Equal(65, watch.Model!.ResumeAtSeconds);   // MaxPositionSeconds من الخادم
            Assert.False(watch.Model.IsReview);
        }

        [Fact]
        public async Task P11_CompletedLecture_IsIdempotent_NoNewCredit()
        {
            using var db = OlcS5Data.Create();
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var clock = new OlcTestClock();
            var svc = Svc(db, clock);
            await WatchToEndAsync(svc, clock, OlcS5Data.Student, c, 600);
            var before = await db.LectureContentProgress.AsNoTracking().SingleAsync();

            clock.Advance(15);
            var again = await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 30));

            Assert.Equal(LecturePingStatus.Ok, again.Status);
            Assert.True(again.Body.Completed);
            Assert.True(again.Body.LectureCompleted);
            var after = await db.LectureContentProgress.AsNoTracking().SingleAsync();
            Assert.Equal(before.WatchedSeconds, after.WatchedSeconds);
            Assert.Equal(before.LastPingAtUtc, after.LastPingAtUtc);
            Assert.Equal(before.CompletedAtUtc, after.CompletedAtUtc);
        }

        [Fact]
        public async Task SameInstantRepeat_GivesNoDoubleCredit()
        {
            using var db = OlcS5Data.Create();
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var clock = new OlcTestClock();
            var svc = Svc(db, clock);

            await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 0));
            clock.Advance(15);
            var a = await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 15));
            var b = await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 15));   // نفس اللحظة (إعادة إرسال)

            Assert.Equal(15, a.Body.WatchedSeconds);
            Assert.Equal(15, b.Body.WatchedSeconds);
        }

        [Fact]
        public async Task Ownership_EachStudentHasOwnRow_NoLeak()
        {
            using var db = OlcS5Data.Create();
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var clock = new OlcTestClock();
            var svc = Svc(db, clock);

            await WatchToEndAsync(svc, clock, OlcS5Data.Student, c, 600);
            var other = await svc.RecordPingAsync(OlcS5Data.OtherStudent, Req(c, "playing", 0));

            Assert.False(other.Body.Completed);   // إتمام طالب آخر لا ينعكس
            Assert.Equal(2, await db.LectureContentProgress.CountAsync(p => p.LectureContentId == c));
            Assert.False(await db.LectureContentProgress.AnyAsync(p => p.StudentId == OlcS5Data.OtherStudent && p.IsCompleted));
        }

        [Fact]
        public async Task IndicatorVideos_R14_ItemLock_NextItemFromServer_LectureCompletesOnLastItem()
        {
            using var db = OlcS5Data.Create();
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1, mode: LectureContentMode.IndicatorVideos, items: 3);
            var next = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);
            var ids = await db.LectureContentItems.AsNoTracking().Where(i => i.LectureContentId == c)
                .OrderBy(i => i.SortOrder).Select(i => i.Id).ToListAsync();
            var clock = new OlcTestClock();
            var svc = Svc(db, clock);

            // itemId إلزامي في IndicatorVideos
            Assert.Equal(LecturePingStatus.BadRequest, (await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 0, 100))).Status);

            // R14: العنصر الثاني قبل إتمام الأول ⇒ locked بلا صف
            var early = await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 0, 100, ids[1]));
            Assert.Equal(LecturePingStatus.Conflict, early.Status);
            Assert.Equal("locked", early.Body.Reason);
            Assert.Equal(StudentLectureProgressService.LockedItemMessage, early.Body.Message);
            Assert.False(await db.LectureContentItemProgress.AnyAsync());

            var r1 = await WatchToEndAsync(svc, clock, OlcS5Data.Student, c, 100, ids[0]);
            Assert.True(r1.Body.Completed);
            Assert.Equal(ids[1], r1.Body.NextItemId);
            Assert.False(r1.Body.LectureCompleted);
            Assert.False(r1.Body.NextLectureUnlocked);

            await WatchToEndAsync(svc, clock, OlcS5Data.Student, c, 100, ids[1]);
            var r3 = await WatchToEndAsync(svc, clock, OlcS5Data.Student, c, 100, ids[2]);
            Assert.True(r3.Body.Completed);
            Assert.True(r3.Body.LectureCompleted);
            Assert.Null(r3.Body.NextItemId);
            Assert.True(r3.Body.NextLectureUnlocked);

            Assert.Equal(LecturePingStatus.Ok, (await svc.RecordPingAsync(OlcS5Data.Student, Req(next, "playing", 0))).Status);
        }

        [Fact]
        public async Task IndicatorVideos_ForeignOrDeletedItem_IsNotFound()
        {
            using var db = OlcS5Data.Create();
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1, mode: LectureContentMode.IndicatorVideos, items: 2);
            var otherContent = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec2, D1, mode: LectureContentMode.IndicatorVideos, items: 1);
            var foreignItem = await db.LectureContentItems.AsNoTracking().Where(i => i.LectureContentId == otherContent).Select(i => i.Id).FirstAsync();
            var first = await db.LectureContentItems.Where(i => i.LectureContentId == c).OrderBy(i => i.SortOrder).FirstAsync();
            first.IsDeleted = true;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var svc = Svc(db, new OlcTestClock());

            Assert.Equal(LecturePingStatus.NotFound, (await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 0, 100, foreignItem))).Status);
            Assert.Equal(LecturePingStatus.NotFound, (await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 0, 100, first.Id))).Status);
        }

        [Fact]
        public async Task CompletedLecture_LockedLaterByOlderPublish_StillIdempotent_NextFollowsSequence()
        {
            using var db = OlcS5Data.Create();
            var b = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D3);
            OlcS5Data.CompleteFull(db, b, OlcS5Data.Student);
            OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);   // أقدم نُشرت لاحقًا ⇒ b مقفلة لكنها مكتملة

            var r = await Svc(db, new OlcTestClock()).RecordPingAsync(OlcS5Data.Student, Req(b, "playing", 0));

            Assert.Equal(LecturePingStatus.Ok, r.Status);
            Assert.True(r.Body.Completed);
            Assert.True(r.Body.LectureCompleted);
            Assert.False(r.Body.NextLectureUnlocked);   // c ما زالت مقفلة حتى إتمام الأقدم
            Assert.Equal(LecturePingStatus.Conflict, (await Svc(db, new OlcTestClock()).RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 0))).Status);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S7.3 — تقرير المحاضرة (الدالة النقية + الخدمة)
    // ═════════════════════════════════════════════════════════════════════
    public class LectureContentReportTests
    {
        private static readonly DateTime D1 = new(2026, 11, 1), D2 = new(2026, 11, 2);
        private static readonly DateTime T = new(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc);

        private static readonly ReportStudent[] Students =
        {
            new(1, "أحمد"), new(2, "بدر"), new(3, "جمال"), new(4, "دانة")
        };

        [Fact]
        public void FullVideo_States_ChapterReach_AndStoppedHere()
        {
            var vm = new LectureContentReportVm { Mode = LectureContentMode.FullVideo, DurationSeconds = 600 };
            var parts = new[]
            {
                new ReportPart(10, "مقدمة", "م1", 0, 120, null),
                new ReportPart(11, null, "م2", 120, 300, null),
                new ReportPart(12, "الخاتمة", "م3", 300, 600, null)
            };
            var full = new[]
            {
                new ReportFullProgress(1, 600, 600, 600, true, T, T, T),
                new ReportFullProgress(2, 200, 200, 600, false, T, T.AddMinutes(5), null),
                new ReportFullProgress(99, 600, 600, 600, true, T, T, T)   // ليس مسجّلًا الآن ⇒ لا يُحتسب
            };

            LectureContentReportBuilder.Fill(vm, Students, new HashSet<int> { 3 }, parts, full, Array.Empty<ReportItemProgress>());

            Assert.Equal(4, vm.EnrolledCount);
            Assert.Equal(2, vm.StartedCount);
            Assert.Equal(1, vm.CompletedCount);
            Assert.Equal(1, vm.InProgressCount);
            Assert.Equal(1, vm.LockedCount);
            Assert.Equal(1, vm.NotStartedCount);

            Assert.Equal(new[] { 2, 1, 1 }, vm.Items.Select(i => i.ReachedEndCount));   // 120 للفصل الأول، 200 < 297 للثاني
            Assert.Equal(new[] { 2, 2, 1 }, vm.Items.Select(i => i.StartedCount));
            Assert.Equal(new[] { 0, 1, 0 }, vm.Items.Select(i => i.StoppedHereCount));
            Assert.Equal("م2", vm.Items[1].Title);   // بلا عنوان ⇒ عنوان المؤشر

            var badr = vm.Students.Single(s => s.StudentId == 2);
            Assert.Equal(LectureReportStudentState.InProgress, badr.State);
            Assert.Equal(33, badr.ProgressPercent);   // floor(200/600)
            Assert.Equal("م2", badr.StoppedAt);
            Assert.Equal(1, badr.CompletedParts);
            Assert.Equal(LectureReportStudentState.InProgress, vm.Students[0].State);   // من يحتاج متابعة أولًا
            Assert.Equal(LectureReportStudentState.Completed, vm.Students[^1].State);
        }

        [Fact]
        public void IndicatorVideos_CompletionNeedsAllItems_StoppedAtFirstIncomplete()
        {
            var vm = new LectureContentReportVm { Mode = LectureContentMode.IndicatorVideos };
            var parts = new[] { new ReportPart(1, "ف1", null, null, null, 100), new ReportPart(2, "ف2", null, null, null, 100) };
            var items = new[]
            {
                new ReportItemProgress(1, 1, 90, true, true, T, T, T), new ReportItemProgress(2, 1, 85, true, true, T, T.AddMinutes(2), T.AddMinutes(2)),
                new ReportItemProgress(1, 2, 90, true, true, T, T, T), new ReportItemProgress(2, 2, 10, true, false, T, T, null),
                new ReportItemProgress(77, 3, 50, true, true, T, T, T)   // عنصر محذوف: لا يُحتسب
            };

            LectureContentReportBuilder.Fill(vm, Students, new HashSet<int>(), parts, Array.Empty<ReportFullProgress>(), items);

            Assert.Equal(1, vm.CompletedCount);
            Assert.Equal(3, vm.StartedCount);
            Assert.Equal(new[] { 2, 1 }, vm.Items.Select(i => i.CompletedCount));
            Assert.Equal(new[] { 2, 2 }, vm.Items.Select(i => i.ReachedEndCount));    // EndedSeen
            Assert.Equal(new[] { 1, 1 }, vm.Items.Select(i => i.StoppedHereCount));   // 2 عند ف2، و3 (بدأ بعنصر محذوف) عند ف1

            var a = vm.Students.Single(s => s.StudentId == 1);
            Assert.Equal(LectureReportStudentState.Completed, a.State);
            Assert.Equal(100, a.ProgressPercent);
            Assert.Equal(175, a.WatchedSeconds);
            Assert.Equal(T.AddMinutes(2), a.CompletedAtUtc);
            Assert.Equal("ف2", vm.Students.Single(s => s.StudentId == 2).StoppedAt);
        }

        [Fact]
        public void LockedStudents_UsesSequenceCalculator()
        {
            var template = new[]
            {
                new SequenceLecture(1, 101, 5, D1, null, false),
                new SequenceLecture(2, 102, 5, D2, null, false)
            };
            var done = new HashSet<(int, int)> { (1, 10) };

            var locked = LectureContentReportBuilder.LockedStudents(2, template, new[] { 10, 20 }, (c, s) => done.Contains((c, s)));

            Assert.Equal(new[] { 20 }, locked);
        }

        [Fact]
        public async Task Service_Report_CountsActiveEnrolledOnly_AndLockFromPreviousLecture()
        {
            using var db = OlcS5Data.Create();
            var first = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var second = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2, mode: LectureContentMode.IndicatorVideos, items: 2);
            OlcS5Data.CompleteFull(db, first, OlcS5Data.Student);
            OlcS5Data.CompleteItems(db, second, OlcS5Data.Student, 1);
            OlcS5Data.CompleteFull(db, first, OlcS5Data.Withdrawn);   // منسحب: لا يُحتسب
            var lectureId = await db.LectureContents.AsNoTracking().Where(c => c.Id == second).Select(c => c.LectureId).FirstAsync();
            var svc = new LectureContentAdminService(db, NullLogger<LectureContentAdminService>.Instance);

            var vm = await svc.GetReportAsync(lectureId);

            Assert.NotNull(vm);
            Assert.Equal(2, vm!.EnrolledCount);            // Student + OtherStudent (Inactive غير نشط للتعلم، Withdrawn منسحب)
            Assert.Equal(1, vm.StartedCount);
            Assert.Equal(0, vm.CompletedCount);
            Assert.Equal(1, vm.LockedCount);               // OtherStudent لم يُتم الأولى
            Assert.Equal(2, vm.SequencePosition);
            Assert.Equal(2, vm.SequenceCount);
            Assert.Equal(2, vm.Items.Count);
            Assert.Equal(1, vm.Items[1].StoppedHereCount);
            Assert.Equal(LectureReportStudentState.Locked, vm.Students.Single(s => s.StudentId == OlcS5Data.OtherStudent).State);

            var firstLecture = await db.LectureContents.AsNoTracking().Where(c => c.Id == first).Select(c => c.LectureId).FirstAsync();
            var firstReport = await svc.GetReportAsync(firstLecture);
            Assert.Equal(1, firstReport!.CompletedCount);
            Assert.Equal(0, firstReport.LockedCount);       // الأولى لا تُقفل أبدًا
            Assert.Equal(1, firstReport.NotStartedCount);

            Assert.Null(await svc.GetReportAsync(999999));
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S7.4 — KPIs الدفعة الأونلاين
    // ═════════════════════════════════════════════════════════════════════
    public class OnlineBatchKpiTests
    {
        private static readonly DateTime L1 = new(2026, 11, 1, 7, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Calculator_PublishedPercent_AverageDelay_AndCompletionPerCurriculum()
        {
            var published = new[]
            {
                new KpiContent(1, 10, "الكمي", LectureContentMode.FullVideo, 0, L1, L1.AddHours(4)),
                new KpiContent(2, 10, "الكمي", LectureContentMode.IndicatorVideos, 2, L1.AddDays(1), L1.AddDays(1).AddHours(8)),
                new KpiContent(3, 20, "اللفظي", LectureContentMode.FullVideo, 0, L1, null)   // بلا PublishedAt: خارج متوسط النشر
            };
            var full = new[] { (1, 100), (1, 200), (1, 999), (3, 100) };                       // 999 غير مسجّل
            var items = new[] { new KpiItemCompletion(2, 100, 2), new KpiItemCompletion(2, 200, 1) }; // 200 لم يُتم كل العناصر

            var k = OnlineBatchKpiCalculator.Compute(4, published, new HashSet<int> { 100, 200 }, full, items);

            Assert.Equal(3, k.PublishedCount);
            Assert.Equal(75, k.PublishedPercent);
            Assert.Equal(6, k.AveragePublishDelayHours);   // (4 + 8) / 2
            Assert.Equal(2, k.PublishDelaySamples);
            Assert.Equal(2, k.EnrolledCount);

            var quant = k.Curricula.Single(c => c.CurriculumId == 10);
            Assert.Equal(2, quant.PublishedLectures);
            Assert.Equal(75, quant.CompletionPercent);     // 3 إتمامات من 2×2
            Assert.Equal(1, quant.StudentsCompletedAll);
            var verbal = k.Curricula.Single(c => c.CurriculumId == 20);
            Assert.Equal(50, verbal.CompletionPercent);
        }

        [Fact]
        public void Calculator_NoPublished_NoDivisionByZero()
        {
            var k = OnlineBatchKpiCalculator.Compute(3, Array.Empty<KpiContent>(), new HashSet<int>(), Array.Empty<(int, int)>(), Array.Empty<KpiItemCompletion>());
            Assert.Equal(0, k.PublishedPercent);
            Assert.Null(k.AveragePublishDelayHours);
            Assert.Empty(k.Curricula);
        }

        [Fact]
        public async Task Service_BatchKpis_FromRealProgress()
        {
            using var db = OlcS5Data.Create();
            var a = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, new DateTime(2026, 11, 1), time: new TimeSpan(18, 0, 0));
            OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec2, new DateTime(2026, 11, 2), status: LectureContentStatus.Draft);
            // أول نشر بعد ساعتين من موعد المحاضرة (18:00 بتوقيت السعودية = 15:00 UTC)
            await db.LectureContents.Where(c => c.Id == a).ForEachAsync(c => c.PublishedAt = new DateTime(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            OlcS5Data.CompleteFull(db, a, OlcS5Data.Student);
            OlcS5Data.CompleteFull(db, a, OlcS5Data.Withdrawn);

            var k = await new LectureContentAdminService(db, NullLogger<LectureContentAdminService>.Instance).GetBatchKpisAsync(OlcS5Data.OnlineBatch);

            Assert.Equal(2, k.LecturesCount);
            Assert.Equal(1, k.PublishedCount);
            Assert.Equal(50, k.PublishedPercent);
            Assert.Equal(2, k.AveragePublishDelayHours);
            Assert.Equal(2, k.EnrolledCount);
            var cur = Assert.Single(k.Curricula);
            Assert.Equal(OlcS5Data.Cur1, cur.CurriculumId);
            Assert.Equal(50, cur.CompletionPercent);
            Assert.Equal(1, cur.StudentsCompletedAll);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S7 — حراس البنية
    // ═════════════════════════════════════════════════════════════════════
    public class OnlineLearningProgressStructureTests
    {
        [Fact]
        public void Ping_IsPost_AntiForgery_RateLimited_NoStudentIdFromRequest()
        {
            var ping = typeof(OnlineLearningController).GetMethod(nameof(OnlineLearningController.Ping))!;
            Assert.NotNull(ping.GetCustomAttribute<HttpPostAttribute>());
            Assert.Null(ping.GetCustomAttribute<HttpGetAttribute>());
            Assert.NotNull(ping.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.Equal("olc-ping", ping.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
            Assert.Equal(typeof(Task<IActionResult>), ping.ReturnType);

            // الجسم هو عقد C.5 فقط — لا studentId/batchId في الطلب
            Assert.DoesNotContain(typeof(StudentLecturePingRequest).GetProperties(), p =>
                p.Name.Contains("Student", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Batch", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(ping.GetParameters(), p => p.ParameterType == typeof(int));
        }

        [Fact]
        public void OlcPingPolicy_Is20PerMinute_KeyedByUser()
        {
            var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "Program.cs"));
            var at = program.IndexOf("o.AddPolicy(\"olc-ping\"", StringComparison.Ordinal);
            Assert.True(at > 0, "سياسة olc-ping غير مسجّلة");
            var block = program.Substring(at, 420);
            Assert.Contains("ctx.User?.Identity?.Name", block);
            Assert.Contains("PermitLimit = 20", block);
            Assert.Contains("TimeSpan.FromMinutes(1)", block);
            Assert.Contains("IStudentLectureProgressService", program);
        }

        [Fact]
        public void PingResponse_UsesContractJsonNames()
        {
            var names = typeof(StudentLecturePingResponse).GetProperties()
                .Select(p => p.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name)
                .ToHashSet();
            foreach (var n in new[] { "ok", "completed", "reason", "message", "watchedSeconds", "requiredSeconds", "nextItemId", "lectureCompleted", "nextLectureUnlocked" })
                Assert.Contains(n, names);
        }

        [Fact]
        public void Report_IsGet_WithReadPermission()
        {
            var report = typeof(QdratNew.Areas.Admin.Controllers.LectureContentsController).GetMethod("Report")!;
            Assert.NotNull(report.GetCustomAttribute<HttpGetAttribute>());
            var perm = report.GetCustomAttribute<AdminPermissionAttribute>();
            Assert.NotNull(perm);
            Assert.Equal("LectureContents:Read", perm!.Policy);
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "QdratNew.csproj")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }
    }
}
