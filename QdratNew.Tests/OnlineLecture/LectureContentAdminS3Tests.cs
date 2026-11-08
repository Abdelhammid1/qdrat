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
    // OLC-S3.1 — قاعدة R4 للفصول (دالة نقية)
    // ═════════════════════════════════════════════════════════════════════
    public class LectureChapterRulesTests
    {
        private static ChapterTiming C(string label, int? start, int? end) => new(label, start, end);

        [Fact]
        public void NoChapters_IsValid_EvenWithoutDuration()
            => Assert.Empty(LectureChapterRules.Validate(Array.Empty<ChapterTiming>(), null));

        [Fact]
        public void Chapters_RequireDuration()
            => Assert.Single(LectureChapterRules.Validate(new[] { C("أ", 0, 60) }, null));

        [Fact]
        public void Valid_Unordered_Input_IsAccepted_AndOrderedByStart()
        {
            var input = new[] { C("ب", 300, 600), C("أ", 0, 300) };
            Assert.Empty(LectureChapterRules.Validate(input, 600));
            Assert.Equal(new[] { "أ", "ب" }, LectureChapterRules.Order(input).Select(c => c.Label));
        }

        [Theory]
        [InlineData(null, 60)]     // بلا بداية
        [InlineData(0, null)]      // بلا نهاية
        [InlineData(-1, 60)]       // سالبة
        [InlineData(60, 60)]       // البداية = النهاية
        [InlineData(90, 60)]       // البداية بعد النهاية
        [InlineData(0, 601)]       // النهاية بعد مدة الفيديو
        public void InvalidTiming_IsRejected(int? start, int? end)
            => Assert.NotEmpty(LectureChapterRules.Validate(new[] { C("أ", start, end) }, 600));

        [Fact]
        public void Overlap_IsRejected_ButTouching_IsAllowed()
        {
            Assert.NotEmpty(LectureChapterRules.Validate(new[] { C("أ", 0, 120), C("ب", 100, 200) }, 600));
            Assert.Empty(LectureChapterRules.Validate(new[] { C("أ", 0, 120), C("ب", 120, 200) }, 600));
        }

        [Fact]
        public void EndEqualToDuration_IsAllowed()
            => Assert.Empty(LectureChapterRules.Validate(new[] { C("أ", 0, 600) }, 600));
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S3 — الخدمة (InMemory): الفصول، فيديوهات المؤشرات، النشر
    // ═════════════════════════════════════════════════════════════════════
    public class LectureContentAdminS3ServiceTests
    {
        private const string Admin = "admin-user-1";
        private const string Admin2 = "admin-user-2";
        private const int OnlineBatch = 10, OfflineBatch = 20;
        private const int Lecture1 = 100, LaterLecture = 101, OfflineLecture = 200;
        private const int SectionA = 1, SectionB = 2;
        // مؤشرات المحور A: 11, 12, 13 (13 غير نشط) — مؤشر المحور B: 21
        private const int LessonA1 = 11, LessonA2 = 12, LessonA3Inactive = 13, LessonB1 = 21;
        private const int RvA1 = 501, RvA1Inactive = 502, RvA2BadUrl = 503, RvB1 = 504, RvA2 = 505;

        private static (LectureContentAdminService Service, ApplicationDbContext Db) Create()
        {
            var db = TestDbContextFactory.CreateFreshContext(out _);
            db.Sections.AddRange(
                new Section { Id = SectionA, Title = "محور الحساب", CurriculumId = 1 },
                new Section { Id = SectionB, Title = "محور الهندسة", CurriculumId = 1 });
            db.Units.Add(new Unit { Id = 1, Title = "الوحدة الأولى" });
            db.Lessons.AddRange(
                new Lesson { Id = LessonA1, Title = "الكسور", Content = "-", UnitId = 1, SectionId = SectionA },
                new Lesson { Id = LessonA2, Title = "النسبة", Content = "-", UnitId = 1, SectionId = SectionA },
                new Lesson { Id = LessonA3Inactive, Title = "مؤشر معطّل", Content = "-", UnitId = 1, SectionId = SectionA, IsActive = false },
                new Lesson { Id = LessonB1, Title = "المثلثات", Content = "-", UnitId = 1, SectionId = SectionB });
            db.RemedialVideos.AddRange(
                new RemedialVideo { Id = RvA1, LessonId = LessonA1, Title = "شرح الكسور", VimeoUrl = "https://vimeo.com/123456789" },
                new RemedialVideo { Id = RvA1Inactive, LessonId = LessonA1, Title = "قديم", VimeoUrl = "https://vimeo.com/223456789", IsActive = false },
                new RemedialVideo { Id = RvA2BadUrl, LessonId = LessonA2, Title = "رابط تالف", VimeoUrl = "https://example.com/video" },
                new RemedialVideo { Id = RvA2, LessonId = LessonA2, Title = "شرح النسبة", VimeoUrl = "https://youtu.be/dQw4w9WgXcQ" },
                new RemedialVideo { Id = RvB1, LessonId = LessonB1, Title = "شرح المثلثات", VimeoUrl = "https://vimeo.com/323456789" });
            db.Batches.AddRange(
                new Batch { Id = OnlineBatch, Name = "دفعة أونلاين", IsOnlineLearning = true },
                new Batch { Id = OfflineBatch, Name = "دفعة حضورية" });
            db.Lecture.AddRange(
                new Lecture { Id = Lecture1, Title = "المحاضرة الأولى", Location = "قاعة", Date = new DateTime(2026, 11, 1), InstructorId = 1, SectionId = SectionA, CourseId = 1, BatchId = OnlineBatch },
                new Lecture { Id = LaterLecture, Title = "المحاضرة الثانية", Location = "قاعة", Date = new DateTime(2026, 11, 8), InstructorId = 1, SectionId = SectionB, CourseId = 1, BatchId = OnlineBatch },
                new Lecture { Id = OfflineLecture, Title = "حضورية", Location = "قاعة", Date = new DateTime(2026, 11, 1), InstructorId = 1, SectionId = SectionA, CourseId = 1, BatchId = OfflineBatch });
            db.SaveChanges();
            db.ChangeTracker.Clear();
            return (new LectureContentAdminService(db, NullLogger<LectureContentAdminService>.Instance), db);
        }

        private static async Task<int> FullVideoAsync(LectureContentAdminService svc, ApplicationDbContext db, int lectureId = Lecture1, string? url = "https://youtu.be/dQw4w9WgXcQ", string? duration = "20:00")
        {
            var id = (await svc.CreateAsync(lectureId, new LectureContentCreateVm { Title = "محتوى", ContentMode = LectureContentMode.FullVideo }, Admin)).Id!.Value;
            var r = await svc.UpdateAsync(id, new LectureContentUpdateVm { Title = "محتوى", ContentMode = LectureContentMode.FullVideo, VideoUrl = url, DurationText = duration }, Admin);
            Assert.True(r.Success, r.Message);
            db.ChangeTracker.Clear();
            return id;
        }

        private static async Task<int> IndicatorAsync(LectureContentAdminService svc, ApplicationDbContext db)
        {
            var id = (await svc.CreateAsync(Lecture1, new LectureContentCreateVm { Title = "قائمة", ContentMode = LectureContentMode.IndicatorVideos }, Admin)).Id!.Value;
            db.ChangeTracker.Clear();
            return id;
        }

        private static LectureContentChapterInputVm Ch(int lessonId, string? start, string? end, bool include = true, string? title = null)
            => new() { LessonId = lessonId, Include = include, StartText = start, EndText = end, Title = title };

        private static AddIndicatorVideoVm FromSource(int lessonId, int sourceId, string? duration = null)
            => new() { LessonId = lessonId, SourceKind = LectureContentSourceKind.RemedialVideo, SourceVideoId = sourceId, DurationText = duration };

        private static AddIndicatorVideoVm Manual(int lessonId, string url)
            => new() { LessonId = lessonId, SourceKind = LectureContentSourceKind.Manual, VideoUrl = url };

        private static async Task<T> Fresh<T>(ApplicationDbContext db, Func<ApplicationDbContext, Task<T>> read)
        {
            db.ChangeTracker.Clear();
            return await read(db);
        }

        // ───────────────────────── S3.1 الفصول ─────────────────────────

        [Fact]
        public async Task SuggestLessons_ReturnsLecturesSectionOnly()
        {
            var (svc, _) = Create();
            var lessons = await svc.SuggestLessonsAsync(Lecture1);
            Assert.Equal(new[] { LessonA1, LessonA2, LessonA3Inactive }, lessons.Select(l => l.Id));
            Assert.False(lessons.Single(l => l.Id == LessonA3Inactive).IsActive);
        }

        [Fact]
        public async Task SetChapters_SavesSortedByStart_WithHindiDigits()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db);

            var r = await svc.SetChaptersAsync(id, new[] { Ch(LessonA2, "١٠:٠٠", "20:00", title: "  النسبة والتناسب "), Ch(LessonA1, "0:00", "10:00"), Ch(LessonA3Inactive, "1:00", "2:00", include: false) }, Admin);

            Assert.True(r.Success, r.Message);
            var items = await Fresh(db, d => d.LectureContentItems.OrderBy(i => i.SortOrder).ToListAsync());
            Assert.Equal(new[] { LessonA1, LessonA2 }, items.Select(i => i.LessonId));
            Assert.Equal(new[] { 1, 2 }, items.Select(i => i.SortOrder));
            Assert.Equal((600, 1200), (items[1].StartSeconds!.Value, items[1].EndSeconds!.Value));
            Assert.Equal("النسبة والتناسب", items[1].Title);
            Assert.All(items, i => { Assert.Null(i.VideoUrl); Assert.Equal(LectureContentSourceKind.Manual, i.SourceVideoKind); Assert.Equal(Admin, i.CreatedBy); });
        }

        [Fact]
        public async Task SetChapters_RejectsOverlap_AndBeyondDuration_R4()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db);

            var overlap = await svc.SetChaptersAsync(id, new[] { Ch(LessonA1, "0:00", "10:00"), Ch(LessonA2, "9:00", "15:00") }, Admin);
            Assert.False(overlap.Success);
            Assert.Contains("يتداخل", overlap.Message);

            var beyond = await svc.SetChaptersAsync(id, new[] { Ch(LessonA1, "0:00", "25:00") }, Admin);
            Assert.False(beyond.Success);
            Assert.Contains("تتجاوز", beyond.Message);

            Assert.Equal(0, await db.LectureContentItems.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task SetChapters_RequiresDuration_AndValidTimes()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db, duration: null);
            var r = await svc.SetChaptersAsync(id, new[] { Ch(LessonA1, "0:00", "1:00") }, Admin);
            Assert.False(r.Success);
            Assert.Contains("مدة", r.Message);

            var (svc2, db2) = Create();
            var id2 = await FullVideoAsync(svc2, db2);
            var bad = await svc2.SetChaptersAsync(id2, new[] { Ch(LessonA1, "abc", "1:00") }, Admin);
            Assert.False(bad.Success);
            Assert.Contains("غير صالحة", bad.Message);
        }

        [Fact]
        public async Task SetChapters_RejectsLessonOutsideSection_R7_AndDuplicates()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db);

            var r7 = await svc.SetChaptersAsync(id, new[] { Ch(LessonB1, "0:00", "1:00") }, Admin);
            Assert.False(r7.Success);
            Assert.Contains("R7", r7.Message);

            var dup = await svc.SetChaptersAsync(id, new[] { Ch(LessonA1, "0:00", "1:00"), Ch(LessonA1, "2:00", "3:00") }, Admin);
            Assert.False(dup.Success);
        }

        [Fact]
        public async Task SetChapters_UncheckedRemoves_AndReaddReactivatesSameRow()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db);
            Assert.True((await svc.SetChaptersAsync(id, new[] { Ch(LessonA1, "0:00", "5:00"), Ch(LessonA2, "5:00", "10:00") }, Admin)).Success);
            var a1Id = await Fresh(db, d => d.LectureContentItems.Where(i => i.LessonId == LessonA1).Select(i => i.Id).SingleAsync());

            Assert.True((await svc.SetChaptersAsync(id, new[] { Ch(LessonA1, "0:00", "5:00", include: false), Ch(LessonA2, "5:00", "10:00") }, Admin)).Success);
            var removed = await Fresh(db, d => d.LectureContentItems.IgnoreQueryFilters().SingleAsync(i => i.Id == a1Id));
            Assert.True(removed.IsDeleted);
            Assert.NotNull(removed.DeletedAt);

            Assert.True((await svc.SetChaptersAsync(id, new[] { Ch(LessonA1, "1:00", "4:00"), Ch(LessonA2, "5:00", "10:00") }, Admin2)).Success);
            var back = await Fresh(db, d => d.LectureContentItems.IgnoreQueryFilters().Where(i => i.LessonId == LessonA1).ToListAsync());
            var row = Assert.Single(back);   // نفس الصف (الفهرس الفريد بلا فلتر)
            Assert.Equal(a1Id, row.Id);
            Assert.False(row.IsDeleted);
            Assert.Equal(60, row.StartSeconds);
            Assert.Equal(Admin2, row.UpdatedBy);
        }

        [Fact]
        public async Task SetChapters_RejectedForIndicatorMode()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            var r = await svc.SetChaptersAsync(id, new[] { Ch(LessonA1, "0:00", "1:00") }, Admin);
            Assert.False(r.Success);
        }

        [Fact]
        public async Task Update_RejectsDurationShorterThanSavedChapters()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db);
            Assert.True((await svc.SetChaptersAsync(id, new[] { Ch(LessonA1, "0:00", "15:00") }, Admin)).Success);

            var shorter = await svc.UpdateAsync(id, new LectureContentUpdateVm { Title = "محتوى", ContentMode = LectureContentMode.FullVideo, VideoUrl = "https://youtu.be/dQw4w9WgXcQ", DurationText = "10:00" }, Admin);
            Assert.False(shorter.Success);
            var cleared = await svc.UpdateAsync(id, new LectureContentUpdateVm { Title = "محتوى", ContentMode = LectureContentMode.FullVideo, VideoUrl = "https://youtu.be/dQw4w9WgXcQ" }, Admin);
            Assert.False(cleared.Success);
            var ok = await svc.UpdateAsync(id, new LectureContentUpdateVm { Title = "محتوى", ContentMode = LectureContentMode.FullVideo, VideoUrl = "https://youtu.be/dQw4w9WgXcQ", DurationText = "15:00" }, Admin);
            Assert.True(ok.Success, ok.Message);
        }

        // ───────────────────────── S3.2 فيديوهات المؤشرات ─────────────────────────

        [Fact]
        public async Task IndicatorOptions_ActiveAndValidOnly_ForLectureSection()
        {
            var (svc, _) = Create();
            var options = await svc.GetIndicatorVideoOptionsAsync(Lecture1);
            Assert.Equal(new[] { RvA1, RvA2 }, options.Select(o => o.Id).OrderBy(x => x));   // لا معطّل، لا رابط تالف، لا محور آخر
            Assert.Equal(LectureVideoProvider.Vimeo, options.Single(o => o.Id == RvA1).Provider);
        }

        [Fact]
        public async Task AddFromRemedialVideo_TakesSnapshot_R6()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);

            var r = await svc.AddIndicatorVideoAsync(id, FromSource(LessonA1, RvA1, "8:30"), Admin);
            Assert.True(r.Success, r.Message);

            var item = await Fresh(db, d => d.LectureContentItems.SingleAsync());
            Assert.Equal(LectureContentSourceKind.RemedialVideo, item.SourceVideoKind);
            Assert.Equal(RvA1, item.SourceVideoId);
            Assert.Equal(LectureVideoProvider.Vimeo, item.VideoProvider);
            Assert.Equal("123456789", item.VideoExternalId);
            Assert.Equal("https://vimeo.com/123456789", item.VideoUrl);
            Assert.Equal(510, item.DurationSeconds);
            Assert.Equal(1, item.SortOrder);

            // تعديل المصدر لاحقًا لا يغيّر اللقطة (R6)
            var source = await db.RemedialVideos.SingleAsync(v => v.Id == RvA1);
            source.VimeoUrl = "https://vimeo.com/987654321";
            await db.SaveChangesAsync();
            Assert.Equal("123456789", (await Fresh(db, d => d.LectureContentItems.SingleAsync())).VideoExternalId);

            var vm = await svc.GetForLectureAsync(Lecture1);
            Assert.True(Assert.Single(vm!.Items).SourceChanged);
        }

        [Theory]
        [InlineData(LessonA1, RvA1Inactive)]   // غير نشط
        [InlineData(LessonA2, RvA1)]           // فيديو مؤشر آخر
        [InlineData(LessonA2, RvA2BadUrl)]     // رابط المصدر يخالف R8
        [InlineData(LessonA1, 9999)]           // غير موجود
        public async Task AddFromRemedialVideo_RejectsInvalidSource(int lessonId, int sourceId)
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            var r = await svc.AddIndicatorVideoAsync(id, FromSource(lessonId, sourceId), Admin);
            Assert.False(r.Success);
            Assert.Equal(0, await db.LectureContentItems.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task AddManual_ValidatesUrl_R8_AndLessonSection_R7()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);

            Assert.False((await svc.AddIndicatorVideoAsync(id, Manual(LessonA1, "http://youtu.be/dQw4w9WgXcQ"), Admin)).Success);
            Assert.False((await svc.AddIndicatorVideoAsync(id, Manual(LessonB1, "https://youtu.be/dQw4w9WgXcQ"), Admin)).Success);
            Assert.False((await svc.AddIndicatorVideoAsync(id, new AddIndicatorVideoVm { LessonId = LessonA1, SourceKind = LectureContentSourceKind.LessonExplanationVideo, SourceVideoId = 1 }, Admin)).Success);

            var ok = await svc.AddIndicatorVideoAsync(id, Manual(LessonA1, "https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=5"), Admin);
            Assert.True(ok.Success, ok.Message);
            var item = await Fresh(db, d => d.LectureContentItems.SingleAsync());
            Assert.Equal(LectureContentSourceKind.Manual, item.SourceVideoKind);
            Assert.Null(item.SourceVideoId);
            Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", item.VideoUrl);
        }

        [Fact]
        public async Task Add_RejectsDuplicateLesson_AndFullVideoMode()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            Assert.True((await svc.AddIndicatorVideoAsync(id, FromSource(LessonA1, RvA1), Admin)).Success);
            var dup = await svc.AddIndicatorVideoAsync(id, Manual(LessonA1, "https://youtu.be/dQw4w9WgXcQ"), Admin);
            Assert.False(dup.Success);
            Assert.Contains("مضاف بالفعل", dup.Message);

            var (svc2, db2) = Create();
            var full = await FullVideoAsync(svc2, db2);
            Assert.False((await svc2.AddIndicatorVideoAsync(full, FromSource(LessonA1, RvA1), Admin)).Success);
        }

        [Fact]
        public async Task Remove_ThenReadd_ReactivatesRow_AtEndOfList()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            await svc.AddIndicatorVideoAsync(id, FromSource(LessonA1, RvA1), Admin);
            await svc.AddIndicatorVideoAsync(id, FromSource(LessonA2, RvA2), Admin);
            var first = await Fresh(db, d => d.LectureContentItems.SingleAsync(i => i.LessonId == LessonA1));

            Assert.True((await svc.RemoveItemAsync(first.Id, Admin)).Success);
            Assert.True((await svc.AddIndicatorVideoAsync(id, Manual(LessonA1, "https://youtu.be/aaaaaaaaaaa"), Admin)).Success);

            var rows = await Fresh(db, d => d.LectureContentItems.IgnoreQueryFilters().Where(i => i.LessonId == LessonA1).ToListAsync());
            var row = Assert.Single(rows);
            Assert.Equal(first.Id, row.Id);
            Assert.False(row.IsDeleted);
            Assert.Equal(LectureContentSourceKind.Manual, row.SourceVideoKind);
            Assert.Null(row.SourceVideoId);
            Assert.Equal(3, row.SortOrder);
        }

        [Fact]
        public async Task Reorder_AppliesExactPermutation_AndRejectsMismatch()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            await svc.AddIndicatorVideoAsync(id, FromSource(LessonA1, RvA1), Admin);
            await svc.AddIndicatorVideoAsync(id, FromSource(LessonA2, RvA2), Admin);
            var ids = await Fresh(db, d => d.LectureContentItems.OrderBy(i => i.SortOrder).Select(i => i.Id).ToListAsync());

            Assert.False((await svc.ReorderItemsAsync(id, new[] { ids[0] }, Admin)).Success);
            Assert.False((await svc.ReorderItemsAsync(id, new[] { ids[0], ids[0] }, Admin)).Success);
            Assert.False((await svc.ReorderItemsAsync(id, new[] { ids[0], 99999 }, Admin)).Success);

            Assert.True((await svc.ReorderItemsAsync(id, new[] { ids[1], ids[0] }, Admin)).Success);
            var order = await Fresh(db, d => d.LectureContentItems.OrderBy(i => i.SortOrder).Select(i => i.LessonId).ToListAsync());
            Assert.Equal(new[] { LessonA2, LessonA1 }, order);
        }

        [Fact]
        public async Task Refresh_UpdatesSnapshotOnlyOnExplicitRequest_R6()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            await svc.AddIndicatorVideoAsync(id, FromSource(LessonA1, RvA1), Admin);
            var itemId = await Fresh(db, d => d.LectureContentItems.Select(i => i.Id).SingleAsync());

            var same = await svc.RefreshItemFromSourceAsync(itemId, Admin);
            Assert.True(same.Success);
            Assert.Contains("مطابقة", same.Message);

            var source = await db.RemedialVideos.SingleAsync(v => v.Id == RvA1);
            source.VimeoUrl = "https://youtu.be/bbbbbbbbbbb";
            await db.SaveChangesAsync();

            Assert.True((await svc.RefreshItemFromSourceAsync(itemId, Admin2)).Success);
            var item = await Fresh(db, d => d.LectureContentItems.SingleAsync());
            Assert.Equal(LectureVideoProvider.YouTube, item.VideoProvider);
            Assert.Equal("bbbbbbbbbbb", item.VideoExternalId);
            Assert.Equal(Admin2, item.UpdatedBy);

            // المصدر عُطّل: اللقطة تبقى
            var s = await db.RemedialVideos.SingleAsync(v => v.Id == RvA1);
            s.IsActive = false;
            await db.SaveChangesAsync();
            Assert.False((await svc.RefreshItemFromSourceAsync(itemId, Admin)).Success);
            Assert.Equal("bbbbbbbbbbb", (await Fresh(db, d => d.LectureContentItems.SingleAsync())).VideoExternalId);
            Assert.True(Assert.Single((await svc.GetForLectureAsync(Lecture1))!.Items).SourceUnavailable);
        }

        [Fact]
        public async Task Refresh_ManualItem_IsRejected()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            await svc.AddIndicatorVideoAsync(id, Manual(LessonA1, "https://youtu.be/dQw4w9WgXcQ"), Admin);
            var itemId = await Fresh(db, d => d.LectureContentItems.Select(i => i.Id).SingleAsync());
            Assert.False((await svc.RefreshItemFromSourceAsync(itemId, Admin)).Success);
        }

        // ───────────────────────── S3.3 النشر ─────────────────────────

        [Fact]
        public async Task Publish_FullVideo_RequiresUrl_R4()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db, url: null);
            var r = await svc.PublishAsync(id, Admin);
            Assert.False(r.Success);
            Assert.Contains("R4", r.Message);
            Assert.Equal(LectureContentStatus.Draft, (await Fresh(db, d => d.LectureContents.SingleAsync())).Status);
        }

        [Fact]
        public async Task Publish_SetsPublishedAtBy_Hide_ThenRepublish_KeepsFirstPublish()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db);

            var r = await svc.PublishAsync(id, Admin);
            Assert.True(r.Success, r.Message);
            var published = await Fresh(db, d => d.LectureContents.SingleAsync());
            Assert.Equal(LectureContentStatus.Published, published.Status);
            Assert.NotNull(published.PublishedAt);
            Assert.Equal(Admin, published.PublishedByUserId);

            Assert.True((await svc.PublishAsync(id, Admin2)).Success);   // idempotent

            Assert.True((await svc.HideAsync(id, Admin2)).Success);
            Assert.Equal(LectureContentStatus.Hidden, (await Fresh(db, d => d.LectureContents.SingleAsync())).Status);

            Assert.True((await svc.PublishAsync(id, Admin2)).Success);
            var again = await Fresh(db, d => d.LectureContents.SingleAsync());
            Assert.Equal(LectureContentStatus.Published, again.Status);
            Assert.Equal(published.PublishedAt, again.PublishedAt);
            Assert.Equal(Admin, again.PublishedByUserId);
            Assert.Equal(Admin2, again.UpdatedBy);
        }

        [Fact]
        public async Task Hide_Draft_IsRejected()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db);
            Assert.False((await svc.HideAsync(id, Admin)).Success);
        }

        [Fact]
        public async Task Publish_Indicator_RequiresAtLeastOneValidItem_R5()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            var empty = await svc.PublishAsync(id, Admin);
            Assert.False(empty.Success);
            Assert.Contains("R5", empty.Message);

            await svc.AddIndicatorVideoAsync(id, FromSource(LessonA1, RvA1), Admin);
            Assert.True((await svc.PublishAsync(id, Admin)).Success);
        }

        [Fact]
        public async Task Publish_RejectsCorruptStoredItem_AndChapterBeyondDuration()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            db.LectureContentItems.Add(new LectureContentItem { LectureContentId = id, LessonId = LessonA1, SortOrder = 1, VideoProvider = LectureVideoProvider.YouTube, VideoExternalId = "bad" });
            await db.SaveChangesAsync();
            Assert.False((await svc.PublishAsync(id, Admin)).Success);

            var (svc2, db2) = Create();
            var full = await FullVideoAsync(svc2, db2, duration: "5:00");
            db2.LectureContentItems.Add(new LectureContentItem { LectureContentId = full, LessonId = LessonA1, SortOrder = 1, StartSeconds = 0, EndSeconds = 900 });
            await db2.SaveChangesAsync();
            Assert.False((await svc2.PublishAsync(full, Admin)).Success);
        }

        [Fact]
        public async Task Publish_RejectsWhenBatchNoLongerOnline_R1()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db);
            var batch = await db.Batches.SingleAsync(b => b.Id == OnlineBatch);
            batch.IsOnlineLearning = false;
            await db.SaveChangesAsync();

            var r = await svc.PublishAsync(id, Admin);
            Assert.False(r.Success);
            Assert.Contains("R1", r.Message);
        }

        [Fact]
        public async Task Remove_LastItemOfPublishedIndicatorContent_IsRejected_R5()
        {
            var (svc, db) = Create();
            var id = await IndicatorAsync(svc, db);
            await svc.AddIndicatorVideoAsync(id, FromSource(LessonA1, RvA1), Admin);
            Assert.True((await svc.PublishAsync(id, Admin)).Success);
            var itemId = await Fresh(db, d => d.LectureContentItems.Select(i => i.Id).SingleAsync());

            var r = await svc.RemoveItemAsync(itemId, Admin);
            Assert.False(r.Success);
            Assert.Equal(1, await Fresh(db, d => d.LectureContentItems.CountAsync()));
        }

        // ───────────────────────── صفحة الأدمن ─────────────────────────

        [Fact]
        public async Task EditVm_FullVideo_BuildsChapterRowsFromSection_AndPublishIssues()
        {
            var (svc, db) = Create();
            var id = await FullVideoAsync(svc, db, url: null);
            await svc.SetChaptersAsync(id, new[] { Ch(LessonA2, "0:00", "5:00") }, Admin);

            var vm = await svc.GetForLectureAsync(Lecture1);
            Assert.Equal(new[] { LessonA1, LessonA2 }, vm!.Chapters.Select(c => c.LessonId));   // المعطّل غير المختار لا يظهر
            Assert.True(vm.Chapters.Single(c => c.LessonId == LessonA2).Include);
            Assert.Equal("0:00", vm.Chapters.Single(c => c.LessonId == LessonA2).StartText);
            Assert.NotEmpty(vm.PublishIssues);   // بلا رابط
            Assert.False(vm.CanPublish);
        }

        [Fact]
        public async Task EditVm_WarnsWhenLaterLectureInCurriculumIsPublished_R15()
        {
            var (svc, db) = Create();
            var later = await FullVideoAsync(svc, db, lectureId: LaterLecture);
            Assert.True((await svc.PublishAsync(later, Admin)).Success);

            await FullVideoAsync(svc, db);
            var vm = await svc.GetForLectureAsync(Lecture1);
            Assert.Contains(vm!.PublishWarnings, w => w.Contains("R15"));
            Assert.True(vm.CanPublish);

            var laterVm = await svc.GetForLectureAsync(LaterLecture);
            Assert.DoesNotContain(laterVm!.PublishWarnings, w => w.Contains("R15"));
        }
    }
}
