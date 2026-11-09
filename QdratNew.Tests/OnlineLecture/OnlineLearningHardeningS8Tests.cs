using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.OnlineLecture;
using QdratNew.ViewModels.OnlineLecture;
using Xunit;

namespace QdratNew.Tests.OnlineLecture
{
    // ═════════════════════════════════════════════════════════════════════
    // OLC-S8.1 — إصلاح المراجعة الأمنية: «المدة من المتصفح قابلة للتزوير»
    // قبل الإصلاح: محتوى بلا مدة من الأدمن ⇒ أول نبضة duration=5 ثم ended بعد 5ث ⇒ إتمام المحاضرة وفتح التالية.
    // بعده: المدة مانع نشر (الفيديو الكامل وكل فيديو مؤشر)، لا تُزال من محتوى منشور، والنبضة بلا مدة أدمن تُرفض بلا كتابة.
    // ═════════════════════════════════════════════════════════════════════
    public class OnlineLearningPingDurationHardeningTests
    {
        private static readonly DateTime D1 = new(2026, 11, 1), D2 = new(2026, 11, 2);

        private static StudentLectureProgressService Svc(ApplicationDbContext db, OlcTestClock clock)
            => new(db, new OnlineLearningAccessService(db, OlcS5Data.Feature(true)), new LectureSequenceService(db), clock,
                NullLogger<StudentLectureProgressService>.Instance);

        private static StudentLecturePingRequest Req(int contentId, string state, double position, double duration, int? itemId = null)
            => new() { LectureContentId = contentId, ItemId = itemId, State = state, Position = position, Duration = duration };

        private static async Task ClearDurationAsync(ApplicationDbContext db, int contentId, bool items)
        {
            if (items)
            {
                foreach (var i in await db.LectureContentItems.Where(i => i.LectureContentId == contentId).ToListAsync())
                    i.DurationSeconds = null;
            }
            else
            {
                (await db.LectureContents.SingleAsync(c => c.Id == contentId)).DurationSeconds = null;
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        [Fact]
        public async Task FullVideo_WithoutAdminDuration_PingIsRefused_AndNothingIsWritten()
        {
            using var db = OlcS5Data.Create();
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var next = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);
            await ClearDurationAsync(db, c, items: false);
            var clock = new OlcTestClock();
            var svc = Svc(db, clock);

            // الهجوم: مدة مزوّرة 5ث ثم ended
            var first = await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 0, 5));
            clock.Advance(6);
            var ended = await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "ended", 5, 5));

            Assert.Equal(LecturePingStatus.NotFound, first.Status);
            Assert.Equal(LecturePingStatus.NotFound, ended.Status);
            Assert.False(ended.Body.Completed);
            Assert.False(await db.LectureContentProgress.AnyAsync());

            // المحاضرة التالية ما زالت مقفلة
            var locked = await svc.RecordPingAsync(OlcS5Data.Student, Req(next, "playing", 0, 600));
            Assert.Equal(LecturePingStatus.Conflict, locked.Status);
        }

        [Fact]
        public async Task FullVideo_WithAdminDuration_ForgedClientDuration_CannotComplete()
        {
            using var db = OlcS5Data.Create();
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);   // مدة الأدمن 600
            var clock = new OlcTestClock();
            var svc = Svc(db, clock);

            await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 0, 5));
            clock.Advance(6);
            var ended = await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "ended", 5, 5));

            Assert.Equal(LecturePingStatus.Ok, ended.Status);
            Assert.False(ended.Body.Completed);
            Assert.Equal("insufficient", ended.Body.Reason);
            Assert.Equal(480, ended.Body.RequiredSeconds);   // ceil(600 × 80%) من مدة الأدمن لا المتصفح
            Assert.False(ended.Body.NextLectureUnlocked);
        }

        [Fact]
        public async Task IndicatorItem_WithoutAdminDuration_PingIsRefused_AndNothingIsWritten()
        {
            using var db = OlcS5Data.Create();
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1, mode: LectureContentMode.IndicatorVideos, items: 1);
            await ClearDurationAsync(db, c, items: true);
            var itemId = await db.LectureContentItems.Where(i => i.LectureContentId == c).Select(i => i.Id).SingleAsync();
            var clock = new OlcTestClock();
            var svc = Svc(db, clock);

            await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "playing", 0, 5, itemId));
            clock.Advance(6);
            var ended = await svc.RecordPingAsync(OlcS5Data.Student, Req(c, "ended", 5, 5, itemId));

            Assert.Equal(LecturePingStatus.NotFound, ended.Status);
            Assert.False(await db.LectureContentItemProgress.AnyAsync());
        }
    }

    public class LectureContentAdminDurationHardeningTests
    {
        private const string Admin = "admin-user-1";
        private const int OnlineBatch = 10, Lecture1 = 100, SectionA = 1, LessonA1 = 11, LessonA2 = 12, RvA1 = 501;

        private static (LectureContentAdminService Service, ApplicationDbContext Db) Create()
        {
            var db = TestDbContextFactory.CreateFreshContext(out _);
            db.Sections.Add(new Section { Id = SectionA, Title = "محور الحساب", CurriculumId = 1 });
            db.Units.Add(new Unit { Id = 1, Title = "الوحدة الأولى" });
            db.Lessons.AddRange(
                new Lesson { Id = LessonA1, Title = "الكسور", Content = "-", UnitId = 1, SectionId = SectionA },
                new Lesson { Id = LessonA2, Title = "النسبة", Content = "-", UnitId = 1, SectionId = SectionA });
            db.RemedialVideos.Add(new RemedialVideo { Id = RvA1, LessonId = LessonA1, Title = "شرح الكسور", VimeoUrl = "https://vimeo.com/123456789" });
            db.Batches.Add(new Batch { Id = OnlineBatch, Name = "دفعة أونلاين", IsOnlineLearning = true });
            db.Lecture.Add(new Lecture { Id = Lecture1, Title = "المحاضرة الأولى", Location = "قاعة", Date = new DateTime(2026, 11, 1), InstructorId = 1, SectionId = SectionA, CourseId = 1, BatchId = OnlineBatch });
            db.SaveChanges();
            db.ChangeTracker.Clear();
            return (new LectureContentAdminService(db, NullLogger<LectureContentAdminService>.Instance), db);
        }

        private static async Task<int> FullVideoAsync(LectureContentAdminService svc, ApplicationDbContext db, string? duration)
        {
            var id = (await svc.CreateAsync(Lecture1, new LectureContentCreateVm { Title = "محتوى", ContentMode = LectureContentMode.FullVideo }, Admin)).Id!.Value;
            var r = await svc.UpdateAsync(id, Update(duration), Admin);
            Assert.True(r.Success, r.Message);
            db.ChangeTracker.Clear();
            return id;
        }

        private static LectureContentUpdateVm Update(string? duration)
            => new() { Title = "محتوى", ContentMode = LectureContentMode.FullVideo, VideoUrl = "https://youtu.be/dQw4w9WgXcQ", DurationText = duration };

        private static async Task<int> IndicatorAsync(LectureContentAdminService svc, ApplicationDbContext db)
        {
            var id = (await svc.CreateAsync(Lecture1, new LectureContentCreateVm { Title = "قائمة", ContentMode = LectureContentMode.IndicatorVideos }, Admin)).Id!.Value;
            db.ChangeTracker.Clear();
            return id;
        }

        private static AddIndicatorVideoVm FromSource(int lessonId, string? duration)
            => new() { LessonId = lessonId, SourceKind = LectureContentSourceKind.RemedialVideo, SourceVideoId = RvA1, DurationText = duration };

        [Fact]
        public async Task Publish_FullVideo_WithoutDuration_IsBlocked_ThenAllowedWithDuration()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db, duration: null);

            var vm = await svc.GetForLectureAsync(Lecture1);
            Assert.False(vm!.CanPublish);
            Assert.Contains(vm.PublishIssues, i => i.Contains("مدة الفيديو"));

            var blocked = await svc.PublishAsync(id, Admin);
            Assert.False(blocked.Success);
            Assert.Contains("مدة الفيديو", blocked.Message);

            Assert.True((await svc.UpdateAsync(id, Update("20:00"), Admin)).Success);
            db.ChangeTracker.Clear();
            Assert.True((await svc.PublishAsync(id, Admin)).Success);
        }

        [Fact]
        public async Task Published_FullVideo_CannotRemoveDuration()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db, duration: "20:00");
            Assert.True((await svc.PublishAsync(id, Admin)).Success);
            db.ChangeTracker.Clear();

            var r = await svc.UpdateAsync(id, Update(null), Admin);
            Assert.False(r.Success);
            Assert.Contains("مدة الفيديو إلزامية", r.Message);
            db.ChangeTracker.Clear();
            Assert.Equal(1200, await db.LectureContents.Where(c => c.Id == id).Select(c => c.DurationSeconds).SingleAsync());
        }

        [Fact]
        public async Task Publish_Indicator_ItemWithoutDuration_IsBlocked()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            Assert.True((await svc.AddIndicatorVideoAsync(id, FromSource(LessonA1, null), Admin)).Success);   // المسودة تقبل
            db.ChangeTracker.Clear();

            var blocked = await svc.PublishAsync(id, Admin);
            Assert.False(blocked.Success);
            Assert.Contains("بلا مدة", blocked.Message);
        }

        [Fact]
        public async Task Published_Indicator_AddItemWithoutDuration_IsRejected_WithDuration_IsAccepted()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            Assert.True((await svc.AddIndicatorVideoAsync(id, FromSource(LessonA1, "5:00"), Admin)).Success);
            db.ChangeTracker.Clear();
            Assert.True((await svc.PublishAsync(id, Admin)).Success);
            db.ChangeTracker.Clear();

            var manual = new AddIndicatorVideoVm { LessonId = LessonA2, SourceKind = LectureContentSourceKind.Manual, VideoUrl = "https://youtu.be/aaaaaaaaaaa" };
            var rejected = await svc.AddIndicatorVideoAsync(id, manual, Admin);
            Assert.False(rejected.Success);
            Assert.Contains("مدة الفيديو", rejected.Message);
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.LectureContentItems.CountAsync(i => i.LectureContentId == id));

            manual.DurationText = "3:30";
            Assert.True((await svc.AddIndicatorVideoAsync(id, manual, Admin)).Success);
        }
    }
}
