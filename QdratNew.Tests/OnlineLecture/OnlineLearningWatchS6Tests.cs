using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Areas.Students.Controllers;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.OnlineLecture;
using QdratNew.ViewModels.OnlineLecture;
using Xunit;

namespace QdratNew.Tests.OnlineLecture
{
    // ═════════════════════════════════════════════════════════════════════
    // OLC-S6 — دوال البناء النقية (R9/R14 + C.7-P4/P6/P11)
    // ═════════════════════════════════════════════════════════════════════
    public class LectureWatchBuilderTests
    {
        private static LectureWatchItemSource Item(int id, bool done, string? ext = "1234567", int? duration = 100, double maxPos = 0)
            => new(id, $"مؤشر {id}", null, id, null, null, LectureVideoProvider.Vimeo, ext, duration, done, maxPos);

        private static LectureWatchItemSource Chapter(int id, int start, int end)
            => new(id, $"مؤشر {id}", null, id, start, end, null, null, null, false, 0);

        [Theory]
        [InlineData(null, 0)]
        [InlineData(0, 0)]
        [InlineData(100, 80)]
        [InlineData(101, 81)]    // ceil(80.8)
        [InlineData(599, 480)]   // ceil(479.2)
        public void RequiredSeconds_IsCeilOf80Percent(int? duration, int expected)
            => Assert.Equal(expected, LectureWatchRules.RequiredSeconds(duration));

        [Fact]
        public void Playlist_R14_UnlocksOnlyAfterPreviousCompleted()
        {
            var items = LectureWatchBuilder.BuildPlaylist(new[] { Item(1, true), Item(2, false), Item(3, false), Item(4, false) }, lectureReview: false);

            Assert.True(items[0].IsUnlocked);
            Assert.True(items[1].IsUnlocked);    // الحالي
            Assert.False(items[2].IsUnlocked);
            Assert.False(items[3].IsUnlocked);
        }

        [Fact]
        public void Playlist_P6_SourceOnlyForCompleted_Current_AndNextDirectly()
        {
            var items = LectureWatchBuilder.BuildPlaylist(new[] { Item(1, true), Item(2, false), Item(3, false), Item(4, false) }, lectureReview: false);

            Assert.NotNull(items[0].Embed);      // مكتمل — مراجعة (P11)
            Assert.NotNull(items[1].Embed);      // المفتوح الحالي
            Assert.NotNull(items[2].Embed);      // التالي مباشرة
            Assert.Null(items[3].Embed);         // ما بعده لا يُكشف
            Assert.Equal("https://player.vimeo.com/video/1234567", items[1].Embed!.EmbedUrl);
        }

        [Fact]
        public void Playlist_LectureReview_AllUnlockedAndExposed_NoResume()
        {
            var items = LectureWatchBuilder.BuildPlaylist(new[] { Item(1, true, maxPos: 90), Item(2, true, maxPos: 90) }, lectureReview: true);
            Assert.All(items, i => Assert.True(i.IsUnlocked));
            Assert.All(items, i => Assert.NotNull(i.Embed));
            Assert.All(items, i => Assert.Equal(0, i.ResumeAtSeconds));
        }

        [Fact]
        public void Playlist_ResumeFromServerMaxPosition_AndRequiredPerItem()
        {
            var items = LectureWatchBuilder.BuildPlaylist(new[] { Item(1, false, duration: 200, maxPos: 47.9) }, lectureReview: false);
            Assert.Equal(47, items[0].ResumeAtSeconds);
            Assert.Equal(160, items[0].RequiredSeconds);
        }

        [Fact]
        public void Playlist_InvalidStoredId_NoEmbed()
        {
            var items = LectureWatchBuilder.BuildPlaylist(new[] { Item(1, false, ext: "not-valid") }, lectureReview: false);
            Assert.True(items[0].IsUnlocked);
            Assert.Null(items[0].Embed);   // R8: لا يُبنى رابط من بيانات غير صالحة
        }

        [Fact]
        public void Chapters_P4_JumpOnlyWithinWatched_ReachedEndWithTolerance()
        {
            var chapters = LectureWatchBuilder.BuildChapters(new[] { Chapter(1, 0, 120), Chapter(2, 120, 300), Chapter(3, 300, 600) }, maxPositionSeconds: 297, freeNavigation: false);

            Assert.True(chapters[0].IsUnlocked);
            Assert.True(chapters[1].IsUnlocked);
            Assert.False(chapters[2].IsUnlocked);      // بدايته 300 > 297
            Assert.True(chapters[0].ReachedEnd);
            Assert.True(chapters[1].ReachedEnd);       // 297 ≥ 300 − 3
            Assert.False(chapters[2].ReachedEnd);
            Assert.All(chapters, c => Assert.Null(c.Embed));   // المصدر واحد للمحتوى، لا لكل فصل
        }

        [Fact]
        public void Chapters_FreeNavigation_AllOpenAndReached()
        {
            var chapters = LectureWatchBuilder.BuildChapters(new[] { Chapter(1, 0, 120), Chapter(2, 120, 600) }, 0, freeNavigation: true);
            Assert.All(chapters, c => Assert.True(c.IsUnlocked && c.ReachedEnd));
        }

        [Fact]
        public void Embed_YouTube_UsesNoCookieHost()
        {
            var e = LectureWatchBuilder.Embed(LectureVideoProvider.YouTube, "dQw4w9WgXcQ");
            Assert.NotNull(e);
            Assert.StartsWith("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", e!.EmbedUrl);
            Assert.Null(LectureWatchBuilder.Embed(null, "dQw4w9WgXcQ"));
            Assert.Null(LectureWatchBuilder.Embed(LectureVideoProvider.YouTube, null));
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S6 — IStudentLectureWatchService (R2 + R15 + P1/P6/P11)
    // ═════════════════════════════════════════════════════════════════════
    public class StudentLectureWatchServiceTests
    {
        private static readonly DateTime D1 = new(2026, 11, 1), D2 = new(2026, 11, 2), D3 = new(2026, 11, 3);

        private static StudentLectureWatchService Watch(ApplicationDbContext db, bool enabled = true)
            => new(db, new OnlineLearningAccessService(db, OlcS5Data.Feature(enabled)), new LectureSequenceService(db));

        [Fact]
        public async Task FeatureOff_OrOtherBatch_OrDraft_IsNotFound()
        {
            using var db = OlcS5Data.Create();
            var mine = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var draft = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2, status: LectureContentStatus.Draft);
            var other = OlcS5Data.Content(db, OlcS5Data.PartnerOkBatch, OlcS5Data.Sec1, D1);

            Assert.Equal(LectureWatchOutcome.NotFound, (await Watch(db, enabled: false).GetWatchAsync(OlcS5Data.Student, mine)).Outcome);
            Assert.Equal(LectureWatchOutcome.NotFound, (await Watch(db).GetWatchAsync(OlcS5Data.Student, draft)).Outcome);
            Assert.Equal(LectureWatchOutcome.NotFound, (await Watch(db).GetWatchAsync(OlcS5Data.Student, other)).Outcome);
            Assert.Equal(LectureWatchOutcome.NotFound, (await Watch(db).GetWatchAsync(OlcS5Data.Withdrawn, mine)).Outcome);
            Assert.Equal(LectureWatchOutcome.NotFound, (await Watch(db).GetWatchAsync(OlcS5Data.Student, 999999)).Outcome);
        }

        [Fact]
        public async Task R15_LockedLecture_ReturnsLocked_WithNoModel()
        {
            using var db = OlcS5Data.Create();
            OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var second = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);

            var result = await Watch(db).GetWatchAsync(OlcS5Data.Student, second);

            Assert.Equal(LectureWatchOutcome.Locked, result.Outcome);
            Assert.Null(result.Model);   // لا أي بيانات فيديو
        }

        [Fact]
        public async Task R13_OtherCurriculum_NotBlockedByIncompleteLecture()
        {
            using var db = OlcS5Data.Create();
            OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var otherCurriculum = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec2, D2);

            Assert.Equal(LectureWatchOutcome.Ok, (await Watch(db).GetWatchAsync(OlcS5Data.Student, otherCurriculum)).Outcome);
        }

        [Fact]
        public async Task FullVideo_Open_WatermarkFromDb_EmbedFromServer_ResumeFromMaxPosition()
        {
            using var db = OlcS5Data.Create();
            var first = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var second = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);
            OlcS5Data.CompleteFull(db, first, OlcS5Data.Student, completed: false, watched: 130.6);

            var result = await Watch(db).GetWatchAsync(OlcS5Data.Student, first);

            Assert.Equal(LectureWatchOutcome.Ok, result.Outcome);
            var vm = result.Model!;
            Assert.False(vm.IsReview);
            Assert.Equal("طالب 1", vm.Watermark.Name);
            Assert.Equal("1000000001", vm.Watermark.NationalId);
            Assert.NotNull(vm.Embed);
            Assert.Equal("dQw4w9WgXcQ", vm.Embed!.ExternalId);
            Assert.Contains("youtube-nocookie.com", vm.Embed.EmbedUrl);
            Assert.Equal(600, vm.DurationSeconds);
            Assert.Equal(480, vm.RequiredSeconds);
            Assert.Equal(130, vm.ResumeAtSeconds);
            Assert.Equal(second, vm.NextLectureContentId);
            Assert.False(vm.NextLectureUnlocked);
        }

        [Fact]
        public async Task P11_CompletedLecture_IsReview_NoResume_NextUnlocked()
        {
            using var db = OlcS5Data.Create();
            var first = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var second = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);
            OlcS5Data.CompleteFull(db, first, OlcS5Data.Student);

            var vm = (await Watch(db).GetWatchAsync(OlcS5Data.Student, first)).Model!;

            Assert.True(vm.IsReview);
            Assert.True(vm.IsCompleted);
            Assert.Equal(0, vm.ResumeAtSeconds);
            Assert.Equal(second, vm.NextLectureContentId);
            Assert.True(vm.NextLectureUnlocked);
        }

        [Fact]
        public async Task CompletedLecture_StillOpenForReview_WhenOlderLecturePublishedLater()
        {
            using var db = OlcS5Data.Create();
            var later = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D3);
            OlcS5Data.CompleteFull(db, later, OlcS5Data.Student);
            OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);   // أقدم نُشرت لاحقًا

            var result = await Watch(db).GetWatchAsync(OlcS5Data.Student, later);
            Assert.Equal(LectureWatchOutcome.Ok, result.Outcome);
            Assert.True(result.Model!.IsReview);
        }

        [Fact]
        public async Task IndicatorVideos_P6_And_R14_FromServer()
        {
            using var db = OlcS5Data.Create();
            var content = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1, LectureContentMode.IndicatorVideos, items: 4);
            OlcS5Data.CompleteItems(db, content, OlcS5Data.Student, 1);

            var vm = (await Watch(db).GetWatchAsync(OlcS5Data.Student, content)).Model!;

            Assert.Equal(LectureContentMode.IndicatorVideos, vm.Mode);
            Assert.Null(vm.Embed);
            Assert.Equal(4, vm.Items.Count);
            Assert.Equal(new[] { true, true, false, false }, vm.Items.Select(i => i.IsUnlocked).ToArray());
            Assert.Equal(new[] { true, true, true, false }, vm.Items.Select(i => i.Embed is not null).ToArray());
            Assert.True(vm.Items[0].IsCompleted);
            Assert.Equal(1, vm.CompletedItems);
        }

        [Fact]
        public async Task IndicatorVideos_SoftDeletedItem_NotListed()
        {
            using var db = OlcS5Data.Create();
            var content = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1, LectureContentMode.IndicatorVideos, items: 3);
            var victim = db.LectureContentItems.Where(i => i.LectureContentId == content).OrderBy(i => i.SortOrder).First();
            victim.IsDeleted = true;
            db.SaveChanges();
            db.ChangeTracker.Clear();

            var vm = (await Watch(db).GetWatchAsync(OlcS5Data.Student, content)).Model!;
            Assert.Equal(2, vm.Items.Count);
            Assert.DoesNotContain(vm.Items, i => i.Id == victim.Id);
        }

        [Fact]
        public async Task OtherStudentsProgress_DoesNotLeak()
        {
            using var db = OlcS5Data.Create();
            var first = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var second = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);
            OlcS5Data.CompleteFull(db, first, OlcS5Data.OtherStudent);

            Assert.Equal(LectureWatchOutcome.Locked, (await Watch(db).GetWatchAsync(OlcS5Data.Student, second)).Outcome);
            var vm = (await Watch(db).GetWatchAsync(OlcS5Data.Student, first)).Model!;
            Assert.False(vm.IsReview);
            Assert.Equal(0, vm.ResumeAtSeconds);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S6 — حراس البنية
    // ═════════════════════════════════════════════════════════════════════
    public class OnlineLearningWatchStructureTests
    {
        [Fact]
        public void Watch_IsGetOnly_NoStore_AcceptsOnlyContentId()
        {
            var watch = typeof(OnlineLearningController).GetMethod(nameof(OnlineLearningController.Watch))!;
            Assert.NotNull(watch.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(watch.GetCustomAttribute<HttpPostAttribute>());
            Assert.Equal(typeof(Task<IActionResult>), watch.ReturnType);

            var cache = watch.GetCustomAttribute<ResponseCacheAttribute>();
            Assert.NotNull(cache);
            Assert.True(cache!.NoStore);

            // studentId/batchId من سياق الخادم فقط — المعامل الوحيد هو معرّف المحتوى
            var ints = watch.GetParameters().Where(p => p.ParameterType == typeof(int)).Select(p => p.Name).ToArray();
            Assert.Equal(new[] { "id" }, ints);
        }

        [Fact]
        public void Watermark_HasNoSettableSourceFromRequest_IsPlainServerVm()
        {
            // العلامة المائية جزء من VM العرض فقط — لا تُربط من نموذج طلب (لا Action يستقبل StudentLectureWatchVm)
            var actions = typeof(OnlineLearningController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.DoesNotContain(actions.SelectMany(a => a.GetParameters()),
                p => p.ParameterType == typeof(StudentLectureWatchVm) || p.ParameterType == typeof(StudentLectureWatermarkVm));
        }

        [Fact]
        public void PlayerScript_ImplementsC7Controls_WithoutOtherProvider()
        {
            var root = FindRepoRoot();
            var js = File.ReadAllText(Path.Combine(root, "wwwroot", "js", "online-lecture-player.js"));

            Assert.Contains("controls: 0", js);                 // P3
            Assert.Contains("disablekb: 1", js);
            Assert.Contains("controls: false", js);             // Vimeo
            Assert.Contains("SKIP_TOLERANCE", js);              // P4
            Assert.Contains("contextmenu", js);                 // P8
            Assert.Contains("'f12'", js);
            Assert.Contains("olc-pseudo-fs", js);               // P9
            Assert.Contains("requestFs(wrapEl)", js);
            Assert.Contains("keepalive: true", js);             // P10
            Assert.DoesNotContain("ping('manual-done')", js);          // لا منصة أخرى في هذه الميزة

            var css = File.ReadAllText(Path.Combine(root, "wwwroot", "css", "student-olc.css"));
            Assert.Contains("@keyframes olc-wm-a", css);        // P1: مساران مختلفان
            Assert.Contains("@keyframes olc-wm-b", css);
            Assert.Contains(".olc-player-shield", css);         // P2
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
