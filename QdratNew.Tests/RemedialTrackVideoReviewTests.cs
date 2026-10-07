using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    /// RTK-S10 (D24): وضع مراجعة الفيديوهات — دالة نقية، خدمة SetVideoReviewAsync، وتسليم الروابط للقراءة فقط في GetAxisAsync.
    /// على InMemory: ExecuteUpdate يُستبدل بمسار التحميل المقابل داخل الخدمة (نفس سلوك CancelAsync).
    /// </summary>
    public class RemedialTrackVideoReviewTests
    {
        private static readonly DateTime T0 = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        private static readonly RemedialTrackActor Actor = new("admin-1", "مدير");
        private static readonly RemedialTrackBatchScope All = RemedialTrackBatchScope.Unrestricted;

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
            public Mock<IAdminActivityLogger> Activity { get; } = new();
            public RemedialTrackPublicationService Publications { get; }
            public RemedialTrackProgressService Progress { get; }

            public const int StudentA = 101;
            public const int StudentB = 202;
            public const int StudentC = 303;
            public int BatchId { get; } = 1;
            public int PublicationId { get; private set; }
            public int OtherPublicationId { get; private set; }
            public int EnrollmentA { get; private set; }
            public int EnrollmentB { get; private set; }
            public int EnrollmentC { get; private set; }     // أمر نشر آخر
            public int PassedAxisProgressA { get; private set; }   // المحور 1: Passed (له فيديوهات مكتملة)
            public int ActiveAxisProgressA { get; private set; }   // المحور 2: Videos (مفتوح)
            public int LockedAxisProgressA { get; private set; }   // المحور 3: Locked
            public int PassedAxisProgressB { get; private set; }

            public Fixture()
            {
                Publications = new RemedialTrackPublicationService(
                    Factory, Clock, Mock.Of<IRemedialTrackCodeGenerator>(), new FakeTz(),
                    Mock.Of<INotificationService>(), Activity.Object,
                    NullLogger<RemedialTrackPublicationService>.Instance);
                Progress = new RemedialTrackProgressService(Factory, Clock, new FakeTz(), NullLogger<RemedialTrackProgressService>.Instance);
            }

            public async Task SeedAsync()
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

                var secs = Enumerable.Range(1, 3).Select(i => new Section { Title = $"قسم {i}", CurriculumId = cur.Id }).ToList();
                db.Sections.AddRange(secs);
                await db.SaveChangesAsync();

                var axes = secs.Select((s, i) => new RemedialTrackAxis
                {
                    TrackId = track.Id, SectionId = s.Id, Order = i + 1, TitleOverride = $"محور {i + 1}", Exam101ModelId = 1, Exam102ModelId = 2
                }).ToList();
                db.RemedialTrackAxes.AddRange(axes);
                await db.SaveChangesAsync();

                var videos = new List<RemedialTrackVideo>
                {
                    new() { AxisId = axes[0].Id, Order = 1, Title = "فيديو 1", Url = "https://youtu.be/aaaaaaaaaaa", Provider = RemedialTrackVideoProvider.YouTube, ExternalId = "aaaaaaaaaaa", DurationSeconds = 100 },
                    new() { AxisId = axes[0].Id, Order = 2, Title = "فيديو 2", Url = "https://youtu.be/bbbbbbbbbbb", Provider = RemedialTrackVideoProvider.YouTube, ExternalId = "bbbbbbbbbbb", DurationSeconds = 100 },
                    new() { AxisId = axes[1].Id, Order = 1, Title = "فيديو 3", Url = "https://youtu.be/ccccccccccc", Provider = RemedialTrackVideoProvider.YouTube, ExternalId = "ccccccccccc", DurationSeconds = 100 }
                };
                db.RemedialTrackVideos.AddRange(videos);

                var pub = NewPublication(track.Id);
                var other = NewPublication(track.Id);
                other.BatchId = 2;
                db.RemedialTrackPublications.AddRange(pub, other);
                await db.SaveChangesAsync();
                PublicationId = pub.Id;
                OtherPublicationId = other.Id;

                EnrollmentA = await AddEnrollmentAsync(db, pub.Id, track.Id, StudentA, axes.Select(a => a.Id).ToArray(), videos);
                EnrollmentB = await AddEnrollmentAsync(db, pub.Id, track.Id, StudentB, axes.Select(a => a.Id).ToArray(), videos);
                EnrollmentC = await AddEnrollmentAsync(db, other.Id, track.Id, StudentC, axes.Select(a => a.Id).ToArray(), videos);

                PassedAxisProgressA = await AxisProgressIdAsync(db, EnrollmentA, 1);
                ActiveAxisProgressA = await AxisProgressIdAsync(db, EnrollmentA, 2);
                LockedAxisProgressA = await AxisProgressIdAsync(db, EnrollmentA, 3);
                PassedAxisProgressB = await AxisProgressIdAsync(db, EnrollmentB, 1);
            }

            private RemedialTrackPublication NewPublication(int trackId) => new()
            {
                TrackId = trackId, BatchId = BatchId, Scope = RemedialTrackPublicationScope.WholeBatch,
                Mode = RemedialTrackDeliveryMode.Online, PublishAtUtc = T0.AddDays(-1), Status = RemedialTrackPublicationStatus.Active,
                CodeVersion = 1, CreatedByUserId = "admin", CreatedAtUtc = T0.AddDays(-2), TotalStudents = 2
            };

            private static Task<int> AxisProgressIdAsync(ApplicationDbContext db, int enrollmentId, int order)
                => db.RemedialTrackAxisProgresses.Where(a => a.EnrollmentId == enrollmentId && a.Order == order).Select(a => a.Id).SingleAsync();

            // المحور 1 اجتيز (فيديوهاته مكتملة)، المحور 2 مفتوح (Videos)، المحور 3 مغلق
            private static async Task<int> AddEnrollmentAsync(ApplicationDbContext db, int pubId, int trackId, int studentId, int[] axisIds, List<RemedialTrackVideo> videos)
            {
                var e = new RemedialTrackEnrollment
                {
                    PublicationId = pubId, TrackId = trackId, StudentId = studentId, CreatedAtUtc = T0.AddDays(-2),
                    Status = RemedialTrackEnrollmentStatus.InProgress, CurrentAxisId = axisIds[1]
                };
                e.AxisProgresses.Add(new RemedialTrackAxisProgress { AxisId = axisIds[0], Order = 1, Status = RemedialTrackAxisStatus.Passed, Round = 1 });
                e.AxisProgresses.Add(new RemedialTrackAxisProgress { AxisId = axisIds[1], Order = 2, Status = RemedialTrackAxisStatus.Videos, Round = 1 });
                e.AxisProgresses.Add(new RemedialTrackAxisProgress { AxisId = axisIds[2], Order = 3, Status = RemedialTrackAxisStatus.Locked, Round = 1 });
                db.RemedialTrackEnrollments.Add(e);
                await db.SaveChangesAsync();

                var passed = e.AxisProgresses.Single(a => a.Order == 1);
                foreach (var v in videos.Where(v => v.AxisId == axisIds[0]))
                    db.RemedialTrackVideoProgresses.Add(new RemedialTrackVideoProgress
                    {
                        AxisProgressId = passed.Id, VideoId = v.Id, VideoOrder = v.Order, Round = 1,
                        WatchedSeconds = 95, DurationSeconds = 100, IsCompleted = true, EndedSeen = true, CompletedAtUtc = T0.AddDays(-1)
                    });
                await db.SaveChangesAsync();
                return e.Id;
            }

            public Task<RemedialTrackResult> ReviewAsync(bool enabled, DateTime? until = null, bool all = false, IEnumerable<int>? ids = null, int? publicationId = null, RemedialTrackBatchScope? scope = null)
                => Publications.SetVideoReviewAsync(publicationId ?? PublicationId,
                    new RemedialTrackReviewInput(ids?.ToList(), all, enabled, until), Actor, scope ?? All);

            public async Task SetAxisStatusAsync(int axisProgressId, RemedialTrackAxisStatus status, int round = 2)
            {
                await using var db = Factory.CreateDbContext();
                var ap = await db.RemedialTrackAxisProgresses.SingleAsync(a => a.Id == axisProgressId);
                ap.Status = status;
                ap.Round = round;
                await db.SaveChangesAsync();
            }

            public async Task<(string Axes, string Videos)> SnapshotAsync()
            {
                await using var db = Factory.CreateDbContext();
                var axes = await db.RemedialTrackAxisProgresses.AsNoTracking().OrderBy(a => a.Id)
                    .Select(a => $"{a.Id}:{a.Status}:{a.Round}:{a.Exam101Percent}:{a.Exam102Percent}").ToListAsync();
                var vids = await db.RemedialTrackVideoProgresses.AsNoTracking().OrderBy(v => v.Id)
                    .Select(v => $"{v.Id}:{v.WatchedSeconds}:{v.IsCompleted}:{v.LastPingAtUtc}:{v.LastPingState}").ToListAsync();
                return (string.Join("|", axes), string.Join("|", vids));
            }
        }

        private static async Task<Fixture> NewAsync()
        {
            var f = new Fixture();
            await f.SeedAsync();
            return f;
        }

        // ═══════════ دالة نقية ═══════════

        [Theory]
        [InlineData(false, null, false)]                       // معطّل
        [InlineData(false, 3600, false)]                       // معطّل حتى مع تاريخ مستقبلي
        [InlineData(true, null, true)]                         // مفتوح بلا انتهاء
        [InlineData(true, 3600, true)]                         // مستقبل
        [InlineData(true, -3600, false)]                       // ماضٍ
        [InlineData(true, 0, false)]                           // لحظة الانتهاء بالضبط = مغلق
        [InlineData(true, 1, true)]                            // ثانية قبل الانتهاء = مفتوح
        public void IsVideoReviewOpen_Table(bool enabled, int? untilOffsetSeconds, bool expected)
        {
            DateTime? until = untilOffsetSeconds.HasValue ? T0.AddSeconds(untilOffsetSeconds.Value) : null;
            Assert.Equal(expected, RemedialTrackStateMachine.IsVideoReviewOpen(enabled, until, T0));
        }

        // ═══════════ SetVideoReviewAsync ═══════════

        [Fact]
        public async Task SetReview_ForSingleEnrollment_OpensOnlyThatStudent()
        {
            var f = await NewAsync();

            var r = await f.ReviewAsync(true, ids: new[] { f.EnrollmentA });

            Assert.True(r.Success, r.Message);
            Assert.Equal(1, r.Data);
            var a = await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA);
            var b = await f.Progress.GetAxisAsync(Fixture.StudentB, f.EnrollmentB, f.PassedAxisProgressB);

            Assert.True(a!.IsReviewMode);
            Assert.False(a.VideosClosed);
            Assert.Equal(2, a.Videos.Count);
            Assert.All(a.Videos, v => { Assert.True(v.IsUnlocked); Assert.False(string.IsNullOrEmpty(v.EmbedUrl)); Assert.NotNull(v.ExternalId); });

            // طالب غير مشمول يبقى مغلقًا (لا تعميم خاطئ)
            Assert.False(b!.IsReviewMode);
            Assert.True(b.VideosClosed);
            Assert.Empty(b.Videos);
        }

        [Fact]
        public async Task SetReview_Selected_Multiple_AndAll_AndDisable()
        {
            var f = await NewAsync();

            Assert.Equal(2, (await f.ReviewAsync(true, ids: new[] { f.EnrollmentA, f.EnrollmentB })).Data);
            Assert.Equal(2, (await f.ReviewAsync(false, all: true)).Data);
            Assert.True((await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA))!.VideosClosed);

            var all = await f.ReviewAsync(true, all: true);
            Assert.True(all.Success, all.Message);
            Assert.Equal(2, all.Data);                                   // الأمر الآخر لا يدخل
            await using var db = f.Factory.CreateDbContext();
            var other = await db.RemedialTrackEnrollments.AsNoTracking().SingleAsync(e => e.Id == f.EnrollmentC);
            Assert.False(other.VideoReviewEnabled);
        }

        [Fact]
        public async Task SetReview_Records_Actor_Time_AndLogsOnce_WithPublicationTag()
        {
            var f = await NewAsync();

            await f.ReviewAsync(true, until: T0.AddDays(2), ids: new[] { f.EnrollmentA });

            await using var db = f.Factory.CreateDbContext();
            var e = await db.RemedialTrackEnrollments.AsNoTracking().SingleAsync(x => x.Id == f.EnrollmentA);
            Assert.True(e.VideoReviewEnabled);
            Assert.Equal(T0.AddDays(2), e.VideoReviewUntilUtc);
            Assert.Equal(T0, e.VideoReviewChangedAtUtc);
            Assert.Equal("admin-1", e.VideoReviewChangedByUserId);
            Assert.Equal("مدير", e.VideoReviewChangedByName);
            f.Activity.Verify(a => a.LogAsync("RemedialTrackVideoReviewChanged",
                It.Is<string>(d => d.Contains($"[RTK pub:{f.PublicationId}]") && d.Contains("تشغيل")),
                "admin-1", "مدير", null, null, f.BatchId), Times.Once);
        }

        [Fact]
        public async Task SetReview_Disable_ClearsUntil()
        {
            var f = await NewAsync();
            await f.ReviewAsync(true, until: T0.AddDays(2), all: true);

            await f.ReviewAsync(false, until: T0.AddDays(5), all: true);   // الانتهاء يُتجاهل مع الإيقاف

            await using var db = f.Factory.CreateDbContext();
            var e = await db.RemedialTrackEnrollments.AsNoTracking().SingleAsync(x => x.Id == f.EnrollmentA);
            Assert.False(e.VideoReviewEnabled);
            Assert.Null(e.VideoReviewUntilUtc);
        }

        [Fact]
        public async Task SetReview_ForeignEnrollmentId_RejectsWholeRequest_NothingChanges()
        {
            var f = await NewAsync();

            var r = await f.ReviewAsync(true, ids: new[] { f.EnrollmentA, f.EnrollmentC });   // C من أمر نشر آخر

            Assert.False(r.Success);
            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(0, await db.RemedialTrackEnrollments.CountAsync(e => e.VideoReviewEnabled));
            f.Activity.Verify(a => a.LogAsync("RemedialTrackVideoReviewChanged", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
        }

        [Fact]
        public async Task SetReview_UnknownId_Rejected()
        {
            var f = await NewAsync();
            Assert.False((await f.ReviewAsync(true, ids: new[] { f.EnrollmentA, 999999 })).Success);
        }

        [Fact]
        public async Task SetReview_Validation_EmptySelection_Over500_PastOrTooFarUntil()
        {
            var f = await NewAsync();

            Assert.False((await f.ReviewAsync(true)).Success);                                              // لا اختيار ولا «الكل»
            Assert.False((await f.ReviewAsync(true, ids: Array.Empty<int>())).Success);
            Assert.False((await f.ReviewAsync(true, ids: Enumerable.Range(1, 501))).Success);              // 501 معرّفًا
            Assert.False((await f.ReviewAsync(true, until: T0.AddSeconds(-1), all: true)).Success);         // ماضٍ
            Assert.False((await f.ReviewAsync(true, until: T0, all: true)).Success);                        // الآن = ليس مستقبلًا
            Assert.False((await f.ReviewAsync(true, until: T0.AddDays(90).AddSeconds(1), all: true)).Success);   // > 90 يومًا
            Assert.True((await f.ReviewAsync(true, until: T0.AddDays(90), all: true)).Success);             // 90 يومًا بالضبط مسموح
        }

        [Fact]
        public async Task SetReview_Exactly500Ids_PassesCountValidation()
        {
            var f = await NewAsync();
            // 500 معرّف (فيهم غريب) → يُرفض لسبب الانتماء لا لسبب الحد
            var r = await f.ReviewAsync(true, ids: Enumerable.Range(1000, 500));
            Assert.False(r.Success);
            Assert.DoesNotContain("الحد الأقصى", r.Message);
        }

        [Fact]
        public async Task SetReview_DeletedCancelledOrOutOfScopePublication_IsRejected()
        {
            var f = await NewAsync();

            Assert.False((await f.ReviewAsync(true, all: true, scope: new RemedialTrackBatchScope(new HashSet<int> { 99 }))).Success);
            Assert.False((await f.ReviewAsync(true, all: true, publicationId: 424242)).Success);

            await f.Publications.CancelAsync(f.OtherPublicationId, "سبب كافٍ", Actor, All);
            Assert.False((await f.ReviewAsync(true, all: true, publicationId: f.OtherPublicationId)).Success);

            await f.Publications.DeleteAsync(f.PublicationId, "سبب كافٍ", Actor, All);
            Assert.False((await f.ReviewAsync(true, all: true)).Success);
        }

        [Fact]
        public async Task SetReview_CancelledEnrollment_IsExcluded_AndRejectedWhenSelectedExplicitly()
        {
            var f = await NewAsync();
            await using (var db = f.Factory.CreateDbContext())
            {
                var e = await db.RemedialTrackEnrollments.SingleAsync(x => x.Id == f.EnrollmentB);
                e.Status = RemedialTrackEnrollmentStatus.Cancelled;
                await db.SaveChangesAsync();
            }

            var all = await f.ReviewAsync(true, all: true);
            Assert.Equal(1, all.Data);                                                       // A فقط
            Assert.False((await f.ReviewAsync(true, ids: new[] { f.EnrollmentB })).Success);
        }

        // ═══════════ GetAxisAsync: حدود الاتاحة ═══════════

        [Fact]
        public async Task Review_LockedAxis_IsNeverReviewable()
        {
            var f = await NewAsync();
            await f.ReviewAsync(true, all: true);

            Assert.Null(await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.LockedAxisProgressA));
        }

        [Fact]
        public async Task Review_DoesNotAffect_CurrentActiveAxis_ItStaysNormalWatching()
        {
            var f = await NewAsync();
            await f.ReviewAsync(true, all: true);

            var vm = await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.ActiveAxisProgressA);

            Assert.True(vm!.CanWatch);
            Assert.False(vm.IsReviewMode);                       // المراجعة للمحاور المنتهية فقط
        }

        [Theory]
        [InlineData(RemedialTrackAxisStatus.FailedBlocked)]
        [InlineData(RemedialTrackAxisStatus.FailedOpenedByAdmin)]
        public async Task Review_FailedAxes_AreClosedByDefault_AndReviewableOnlyWhenAdminEnables(RemedialTrackAxisStatus status)
        {
            var f = await NewAsync();
            await f.SetAxisStatusAsync(f.PassedAxisProgressA, status);
            // المحور له فيديوهات الجولة 2 (التي تُعرض لحالات الفشل)
            await using (var db = f.Factory.CreateDbContext())
            {
                var rows = await db.RemedialTrackVideoProgresses.AsNoTracking().Where(v => v.AxisProgressId == f.PassedAxisProgressA).ToListAsync();
                foreach (var v in rows)
                    db.RemedialTrackVideoProgresses.Add(new RemedialTrackVideoProgress
                    {
                        AxisProgressId = v.AxisProgressId, VideoId = v.VideoId, VideoOrder = v.VideoOrder, Round = 2,
                        WatchedSeconds = 95, DurationSeconds = 100, IsCompleted = true
                    });
                await db.SaveChangesAsync();
            }

            var before = await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA);
            Assert.True(before!.VideosClosed);
            Assert.Empty(before.Videos);

            await f.ReviewAsync(true, ids: new[] { f.EnrollmentA });
            var after = await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA);

            Assert.True(after!.IsReviewMode);
            Assert.Equal(2, after.Videos.Count);
        }

        [Fact]
        public async Task Review_Expires_ServerSide_AtRequestTime()
        {
            var f = await NewAsync();
            await f.ReviewAsync(true, until: T0.AddHours(1), ids: new[] { f.EnrollmentA });

            f.Clock.Now = T0.AddMinutes(59);
            var open = await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA);
            Assert.True(open!.IsReviewMode);
            Assert.Equal(T0.AddHours(1).AddHours(3), open.ReviewUntilLocal);            // بتوقيت السعودية

            f.Clock.Now = T0.AddHours(1);                                               // لحظة الانتهاء ⇒ مغلق فورًا
            var closed = await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA);
            Assert.False(closed!.IsReviewMode);
            Assert.True(closed.VideosClosed);
            Assert.Empty(closed.Videos);
            Assert.All(closed.Videos, v => Assert.Null(v.ExternalId));
        }

        [Fact]
        public async Task Review_OpenEnded_HasNoUntil_AndClosesWhenAdminDisables()
        {
            var f = await NewAsync();
            await f.ReviewAsync(true, ids: new[] { f.EnrollmentA });

            f.Clock.Now = T0.AddDays(30);
            var vm = await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA);
            Assert.True(vm!.IsReviewMode);
            Assert.Null(vm.ReviewUntilLocal);

            await f.ReviewAsync(false, ids: new[] { f.EnrollmentA });
            Assert.True((await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA))!.VideosClosed);
        }

        [Fact]
        public async Task Review_OtherStudent_CannotReachReviewedAxis_Idor()
        {
            var f = await NewAsync();
            await f.ReviewAsync(true, all: true);

            Assert.Null(await f.Progress.GetAxisAsync(Fixture.StudentB, f.EnrollmentA, f.PassedAxisProgressA));
        }

        [Fact]
        public async Task Review_CancelledEnrollmentOrPublication_DoesNotDeliverLinks()
        {
            var f = await NewAsync();
            await f.ReviewAsync(true, all: true);

            await f.Publications.CancelAsync(f.PublicationId, "سبب كافٍ", Actor, All);
            var vm = await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA);

            Assert.False(vm!.IsReviewMode);
            Assert.Empty(vm.Videos);
        }

        [Fact]
        public async Task Review_DeletedPublication_IsNotAvailable()
        {
            var f = await NewAsync();
            await f.ReviewAsync(true, all: true);
            await f.Publications.DeleteAsync(f.PublicationId, "سبب كافٍ", Actor, All);

            Assert.Null(await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA));
        }

        // ═══════════ للقراءة فقط: لا كتابة تقدّم، والنبضة تُرفض ═══════════

        [Fact]
        public async Task Review_ViewingNeverWritesProgressOrAxisState()
        {
            var f = await NewAsync();
            await f.ReviewAsync(true, ids: new[] { f.EnrollmentA });
            // يُنشأ محور 2 المفتوح مسبقًا حتى لا يختلط بصفوف إنشاء كسولة غير متعلقة بالمراجعة
            await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.ActiveAxisProgressA);
            var before = await f.SnapshotAsync();

            for (var i = 0; i < 3; i++)
            {
                f.Clock.Now = T0.AddMinutes(i + 1);
                var vm = await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA);
                Assert.True(vm!.IsReviewMode);
            }

            Assert.Equal(before, await f.SnapshotAsync());
        }

        [Fact]
        public async Task Review_PingsAreRejected_NoProgressChange()
        {
            var f = await NewAsync();
            await f.ReviewAsync(true, ids: new[] { f.EnrollmentA });
            var review = await f.Progress.GetAxisAsync(Fixture.StudentA, f.EnrollmentA, f.PassedAxisProgressA);
            var vpId = review!.Videos[0].VideoProgressId;
            var before = await f.SnapshotAsync();

            foreach (var state in new[] { "playing", "ended", "manual-done" })
            {
                var res = await f.Progress.RecordPingAsync(Fixture.StudentA, new RemedialTrackVideoPingRequest
                {
                    EnrollmentId = f.EnrollmentA, VideoProgressId = vpId, State = state, Duration = 100, Position = 100
                });
                Assert.NotEqual(RemedialTrackPingStatus.Ok, res.Status);
            }

            Assert.Equal(before, await f.SnapshotAsync());
        }
    }
}
