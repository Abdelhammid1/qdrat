using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Areas.Admin.Controllers;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Security.Permissions;
using QdratNew.Services.OnlineLecture;
using QdratNew.ViewModels.OnlineLecture;
using Xunit;

namespace QdratNew.Tests.OnlineLecture
{
    // ═════════════════════════════════════════════════════════════════════
    // OLC-S2.2 — محلّل الروابط (R8) — دالة نقية
    // ═════════════════════════════════════════════════════════════════════
    public class LectureVideoUrlParserTests
    {
        [Theory]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ&t=30s", "dQw4w9WgXcQ")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("  https://youtu.be/a_B-c1d2E3f  ", "a_B-c1d2E3f")]
        public void YouTube_Valid(string url, string expectedId)
        {
            Assert.True(LectureVideoUrlParser.TryParse(url, out var provider, out var id, out var error), error);
            Assert.Equal(LectureVideoProvider.YouTube, provider);
            Assert.Equal(expectedId, id);
        }

        [Theory]
        [InlineData("https://vimeo.com/123456789", "123456789")]
        [InlineData("https://player.vimeo.com/video/76979871", "76979871")]
        public void Vimeo_Valid(string url, string expectedId)
        {
            Assert.True(LectureVideoUrlParser.TryParse(url, out var provider, out var id, out var error), error);
            Assert.Equal(LectureVideoProvider.Vimeo, provider);
            Assert.Equal(expectedId, id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ")]          // https فقط
        [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]           // خارج القائمة الثابتة
        [InlineData("https://youtube.com.evil.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://evil.com/?u=https://youtu.be/dQw4w9WgXcQ")]
        [InlineData("https://user:pass@youtube.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://youtube.com:8443/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/watch?v=short")]                // معرّف ليس 11 حرفًا
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXc\"")]
        [InlineData("https://www.youtube.com/playlist?list=PL123")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ/extra")]
        [InlineData("https://vimeo.com/12345")]                               // أقل من 6 أرقام
        [InlineData("https://vimeo.com/123456789/abcdef1234")]                // رابط خاص بـ hash
        [InlineData("https://player.vimeo.com/video/123456789?h=abcdef1234")]
        [InlineData("https://vimeo.com/channels/staffpicks")]
        [InlineData("javascript:alert(1)")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w\n9WgXcQ")]
        [InlineData("https://www.you tube.com/watch?v=dQw4w9WgXcQ")]
        public void Invalid_IsRejected_WithArabicMessage(string? url)
        {
            Assert.False(LectureVideoUrlParser.TryParse(url, out _, out var id, out var error));
            Assert.Equal(string.Empty, id);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        [Fact]
        public void TooLongUrl_IsRejected()
            => Assert.False(LectureVideoUrlParser.TryParse("https://youtu.be/" + new string('a', 600), out _, out _));

        [Fact]
        public void BuildEmbedUrl_IsBuiltFromIdOnly()
        {
            Assert.Equal("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?enablejsapi=1&rel=0",
                LectureVideoUrlParser.BuildEmbedUrl(LectureVideoProvider.YouTube, "dQw4w9WgXcQ"));
            Assert.Equal("https://player.vimeo.com/video/123456789",
                LectureVideoUrlParser.BuildEmbedUrl(LectureVideoProvider.Vimeo, "123456789"));
        }

        [Fact]
        public void BuildEmbedUrl_RejectsTamperedIds()
        {
            Assert.Throws<ArgumentException>(() => LectureVideoUrlParser.BuildEmbedUrl(LectureVideoProvider.YouTube, "x\"><script>"));
            Assert.Throws<ArgumentException>(() => LectureVideoUrlParser.BuildEmbedUrl(LectureVideoProvider.Vimeo, "dQw4w9WgXcQ"));
            Assert.Throws<ArgumentOutOfRangeException>(() => LectureVideoUrlParser.BuildEmbedUrl((LectureVideoProvider)9, "123456789"));
            Assert.Null(LectureVideoUrlParser.TryBuildEmbedUrl(LectureVideoProvider.YouTube, "bad"));
            Assert.Null(LectureVideoUrlParser.TryBuildEmbedUrl(null, "dQw4w9WgXcQ"));
        }

        [Fact]
        public void CanonicalUrl_IsRebuiltFromId()
        {
            Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                LectureVideoUrlParser.BuildCanonicalUrl(LectureVideoProvider.YouTube, "dQw4w9WgXcQ"));
            Assert.Equal("https://vimeo.com/123456789",
                LectureVideoUrlParser.BuildCanonicalUrl(LectureVideoProvider.Vimeo, "123456789"));
        }

        [Fact]
        public void AllowedHosts_AreExactlyTheContractList()
            => Assert.Equal(new[] { "youtube.com", "www.youtube.com", "youtu.be", "vimeo.com", "player.vimeo.com" },
                            LectureVideoUrlParser.AllowedHosts);
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S2 — صيغة الوقت mm:ss / hh:mm:ss
    // ═════════════════════════════════════════════════════════════════════
    public class LectureTimeFormatTests
    {
        [Theory]
        [InlineData("90", 90)]
        [InlineData("1:30", 90)]
        [InlineData("01:02:03", 3723)]
        [InlineData("١:٣٠", 90)]
        [InlineData(" 45:00 ", 2700)]
        [InlineData("12:00:00", 43200)]
        public void Parse_Valid(string text, int expected)
        {
            Assert.True(LectureTimeFormat.TryParse(text, out var seconds));
            Assert.Equal(expected, seconds);
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("1:60")]
        [InlineData("1:2:3:4")]
        [InlineData("-5")]
        [InlineData("12:00:01")]
        [InlineData("1::30")]
        public void Parse_Invalid(string text)
            => Assert.False(LectureTimeFormat.TryParse(text, out _));

        [Theory]
        [InlineData(90, "1:30")]
        [InlineData(3723, "1:02:03")]
        [InlineData(5, "0:05")]
        public void Format_RoundTrips(int seconds, string expected)
        {
            Assert.Equal(expected, LectureTimeFormat.Format(seconds));
            Assert.True(LectureTimeFormat.TryParse(expected, out var back));
            Assert.Equal(seconds, back);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S2.1/S2.2 — الخدمة (InMemory)
    // ═════════════════════════════════════════════════════════════════════
    public class LectureContentAdminServiceTests
    {
        private const string Admin = "admin-user-1";
        private const int OnlineBatch = 10, OfflineBatch = 20, ArchivedBatch = 30;
        private const int OnlineLecture = 100, OfflineLecture = 200;

        private static (LectureContentAdminService Service, ApplicationDbContext Db) Create()
        {
            var db = TestDbContextFactory.CreateFreshContext(out _);
            db.Sections.Add(new Section { Id = 1, Title = "محور الحساب", CurriculumId = 1 });
            db.Batches.AddRange(
                new Batch { Id = OnlineBatch, Name = "دفعة أونلاين", IsOnlineLearning = true, OnlineLearningEnabledAt = DateTime.UtcNow.AddDays(-1) },
                new Batch { Id = OfflineBatch, Name = "دفعة حضورية" },
                new Batch { Id = ArchivedBatch, Name = "دفعة مؤرشفة", IsArchived = true });
            db.Lecture.AddRange(
                new Lecture { Id = OnlineLecture, Title = "المحاضرة الأولى", Location = "قاعة 1", Date = DateTime.Today, InstructorId = 1, SectionId = 1, CourseId = 1, BatchId = OnlineBatch },
                new Lecture { Id = OfflineLecture, Title = "محاضرة حضورية", Location = "قاعة 2", Date = DateTime.Today, InstructorId = 1, SectionId = 1, CourseId = 1, BatchId = OfflineBatch });
            db.SaveChanges();
            db.ChangeTracker.Clear();
            return (new LectureContentAdminService(db, NullLogger<LectureContentAdminService>.Instance), db);
        }

        private static LectureContentCreateVm NewDraft(LectureContentMode mode = LectureContentMode.FullVideo)
            => new() { Title = "  شرح الكسور  ", ContentMode = mode };

        private static LectureContentUpdateVm Update(string? url = null, LectureContentMode mode = LectureContentMode.FullVideo, string? duration = null, bool confirm = false)
            => new() { Title = "شرح الكسور", ContentMode = mode, VideoUrl = url, DurationText = duration, ConfirmModeChange = confirm };

        // ── R1 ──

        [Fact]
        public async Task Create_RejectsLectureOfNonOnlineBatch_R1()
        {
            var (svc, db) = Create();
            var r = await svc.CreateAsync(OfflineLecture, NewDraft(), Admin);
            Assert.False(r.Success);
            Assert.Contains("أونلاين", r.Message);
            Assert.Equal(0, await db.LectureContents.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task Create_MissingLecture_IsNotFound()
        {
            var (svc, _) = Create();
            var r = await svc.CreateAsync(999, NewDraft(), Admin);
            Assert.True(r.NotFound);
        }

        [Fact]
        public async Task Create_MakesTrimmedDraft_WithAudit()
        {
            var (svc, db) = Create();
            var r = await svc.CreateAsync(OnlineLecture, NewDraft(LectureContentMode.IndicatorVideos), Admin);
            Assert.True(r.Success, r.Message);

            var c = await db.LectureContents.AsNoTracking().SingleAsync();
            Assert.Equal(r.Id, c.Id);
            Assert.Equal(OnlineLecture, c.LectureId);
            Assert.Equal(LectureContentStatus.Draft, c.Status);
            Assert.Equal(LectureContentMode.IndicatorVideos, c.ContentMode);
            Assert.Equal("شرح الكسور", c.Title);
            Assert.Equal(Admin, c.CreatedBy);
            Assert.Null(c.PublishedAt);
        }

        [Fact]
        public async Task Create_IsIdempotent_SecondCallReturnsSameId()
        {
            var (svc, db) = Create();
            var first = await svc.CreateAsync(OnlineLecture, NewDraft(), Admin);
            var second = await svc.CreateAsync(OnlineLecture, NewDraft(), Admin);
            Assert.True(second.Success);
            Assert.Equal(first.Id, second.Id);
            Assert.Equal(1, await db.LectureContents.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task Create_ReactivatesSoftDeletedRow_InsteadOfNewRow()
        {
            var (svc, db) = Create();
            db.LectureContents.Add(new LectureContent
            {
                Id = 7, LectureId = OnlineLecture, ContentMode = LectureContentMode.FullVideo, Status = LectureContentStatus.Published,
                Title = "قديم", VideoProvider = LectureVideoProvider.YouTube, VideoExternalId = "dQw4w9WgXcQ",
                PublishedAt = DateTime.UtcNow, PublishedByUserId = "x", IsDeleted = true, DeletedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var r = await svc.CreateAsync(OnlineLecture, NewDraft(LectureContentMode.IndicatorVideos), Admin);
            Assert.True(r.Success, r.Message);
            Assert.Equal(7, r.Id);

            var c = await db.LectureContents.AsNoTracking().SingleAsync();
            Assert.False(c.IsDeleted);
            Assert.Equal(LectureContentStatus.Draft, c.Status);
            Assert.Equal(LectureContentMode.IndicatorVideos, c.ContentMode);
            Assert.Null(c.VideoExternalId);
            Assert.Null(c.PublishedAt);
        }

        [Fact]
        public async Task Create_RejectsEmptyTitleAndUndefinedMode()
        {
            var (svc, _) = Create();
            Assert.False((await svc.CreateAsync(OnlineLecture, new LectureContentCreateVm { Title = "  ", ContentMode = LectureContentMode.FullVideo }, Admin)).Success);
            Assert.False((await svc.CreateAsync(OnlineLecture, new LectureContentCreateVm { Title = "x", ContentMode = (LectureContentMode)9 }, Admin)).Success);
        }

        // ── R8 + الفيديو الكامل ──

        private static async Task<int> DraftAsync(LectureContentAdminService svc, LectureContentMode mode = LectureContentMode.FullVideo)
            => (await svc.CreateAsync(OnlineLecture, NewDraft(mode), Admin)).Id!.Value;

        [Fact]
        public async Task Update_StoresProviderIdAndCanonicalUrl_R8()
        {
            var (svc, db) = Create();
            var id = await DraftAsync(svc);

            var r = await svc.UpdateAsync(id, Update("https://youtu.be/dQw4w9WgXcQ?si=tracking", duration: "1:25:30"), Admin);
            Assert.True(r.Success, r.Message);

            var c = await db.LectureContents.AsNoTracking().SingleAsync();
            Assert.Equal(LectureVideoProvider.YouTube, c.VideoProvider);
            Assert.Equal("dQw4w9WgXcQ", c.VideoExternalId);
            Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", c.VideoUrl);
            Assert.Equal(5130, c.DurationSeconds);
            Assert.Equal(Admin, c.UpdatedBy);
            Assert.NotNull(c.UpdatedAt);
        }

        [Fact]
        public async Task Update_RejectsUnsafeUrl_AndKeepsOldValue()
        {
            var (svc, db) = Create();
            var id = await DraftAsync(svc);
            await svc.UpdateAsync(id, Update("https://vimeo.com/123456789"), Admin);

            var r = await svc.UpdateAsync(id, Update("https://evil.example/video.mp4"), Admin);
            Assert.False(r.Success);

            var c = await db.LectureContents.AsNoTracking().SingleAsync();
            Assert.Equal(LectureVideoProvider.Vimeo, c.VideoProvider);
            Assert.Equal("123456789", c.VideoExternalId);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("0")]
        [InlineData("13:00:00")]
        public async Task Update_RejectsInvalidDuration(string duration)
        {
            var (svc, _) = Create();
            var id = await DraftAsync(svc);
            Assert.False((await svc.UpdateAsync(id, Update("https://vimeo.com/123456789", duration: duration), Admin)).Success);
        }

        [Fact]
        public async Task Update_RejectsNonHttpsThumbnail()
        {
            var (svc, _) = Create();
            var id = await DraftAsync(svc);
            var vm = Update();
            vm.ThumbnailUrl = "javascript:alert(1)";
            Assert.False((await svc.UpdateAsync(id, vm, Admin)).Success);
            vm.ThumbnailUrl = "http://img.example/x.jpg";
            Assert.False((await svc.UpdateAsync(id, vm, Admin)).Success);
            vm.ThumbnailUrl = "https://img.example/x.jpg";
            Assert.True((await svc.UpdateAsync(id, vm, Admin)).Success);
        }

        [Fact]
        public async Task Update_PublishedFullVideo_CannotDropUrl_R4()
        {
            var (svc, db) = Create();
            var id = await DraftAsync(svc);
            await svc.UpdateAsync(id, Update("https://youtu.be/dQw4w9WgXcQ"), Admin);
            await SetStatusAsync(db, id, LectureContentStatus.Published);

            var r = await svc.UpdateAsync(id, Update(url: null), Admin);
            Assert.False(r.Success);
            Assert.Equal("dQw4w9WgXcQ", (await db.LectureContents.AsNoTracking().SingleAsync()).VideoExternalId);
        }

        [Fact]
        public async Task Update_Missing_IsNotFound()
        {
            var (svc, _) = Create();
            Assert.True((await svc.UpdateAsync(12345, Update(), Admin)).NotFound);
        }

        // ── تغيير الوضع ──

        private static async Task SetStatusAsync(ApplicationDbContext db, int id, LectureContentStatus status)
        {
            var c = await db.LectureContents.SingleAsync(x => x.Id == id);
            c.Status = status;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        private static async Task AddItemsAsync(ApplicationDbContext db, int contentId, int count)
        {
            for (var i = 1; i <= count; i++)
                db.LectureContentItems.Add(new LectureContentItem { LectureContentId = contentId, LessonId = i, SortOrder = i, StartSeconds = (i - 1) * 60, EndSeconds = i * 60 });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        [Fact]
        public async Task ModeChange_WithItems_RequiresConfirmation_ThenSoftDeletesItems()
        {
            var (svc, db) = Create();
            var id = await DraftAsync(svc);
            await svc.UpdateAsync(id, Update("https://youtu.be/dQw4w9WgXcQ", duration: "10:00"), Admin);
            await AddItemsAsync(db, id, 3);

            var refused = await svc.UpdateAsync(id, Update(mode: LectureContentMode.IndicatorVideos), Admin);
            Assert.False(refused.Success);
            Assert.Contains("3", refused.Message);
            Assert.Equal(3, await db.LectureContentItems.CountAsync());

            var ok = await svc.UpdateAsync(id, Update(mode: LectureContentMode.IndicatorVideos, confirm: true), Admin);
            Assert.True(ok.Success, ok.Message);

            Assert.Equal(0, await db.LectureContentItems.CountAsync());                       // QueryFilter
            var all = await db.LectureContentItems.IgnoreQueryFilters().AsNoTracking().ToListAsync();
            Assert.Equal(3, all.Count);
            Assert.All(all, i => { Assert.True(i.IsDeleted); Assert.NotNull(i.DeletedAt); Assert.Equal(Admin, i.UpdatedBy); });

            // الحقول على مستوى المحتوى للوضع FullVideo فقط
            var c = await db.LectureContents.AsNoTracking().SingleAsync();
            Assert.Equal(LectureContentMode.IndicatorVideos, c.ContentMode);
            Assert.Null(c.VideoProvider);
            Assert.Null(c.VideoUrl);
            Assert.Null(c.VideoExternalId);
            Assert.Null(c.DurationSeconds);
        }

        [Fact]
        public async Task ModeChange_WithoutItems_NeedsNoConfirmation()
        {
            var (svc, _) = Create();
            var id = await DraftAsync(svc);
            Assert.True((await svc.UpdateAsync(id, Update(mode: LectureContentMode.IndicatorVideos), Admin)).Success);
        }

        [Fact]
        public async Task ModeChange_IsBlockedWhilePublished()
        {
            var (svc, db) = Create();
            var id = await DraftAsync(svc);
            await svc.UpdateAsync(id, Update("https://youtu.be/dQw4w9WgXcQ"), Admin);
            await SetStatusAsync(db, id, LectureContentStatus.Published);

            var r = await svc.UpdateAsync(id, Update(mode: LectureContentMode.IndicatorVideos, confirm: true), Admin);
            Assert.False(r.Success);
            Assert.Equal(LectureContentMode.FullVideo, (await db.LectureContents.AsNoTracking().SingleAsync()).ContentMode);
        }

        // ── الدفعة الأونلاين (S2.1) ──

        [Fact]
        public async Task EnableBatch_SetsFlagAndTimestamp()
        {
            var (svc, db) = Create();
            var before = DateTime.UtcNow.AddSeconds(-1);
            var r = await svc.SetBatchOnlineLearningAsync(OfflineBatch, true);
            Assert.True(r.Success, r.Message);

            var b = await db.Batches.AsNoTracking().SingleAsync(x => x.Id == OfflineBatch);
            Assert.True(b.IsOnlineLearning);
            Assert.True(b.OnlineLearningEnabledAt >= before);
        }

        [Fact]
        public async Task EnableBatch_RejectsArchived()
        {
            var (svc, db) = Create();
            Assert.False((await svc.SetBatchOnlineLearningAsync(ArchivedBatch, true)).Success);
            Assert.False((await db.Batches.AsNoTracking().SingleAsync(x => x.Id == ArchivedBatch)).IsOnlineLearning);
        }

        [Fact]
        public async Task DisableBatch_KeepsContent_AndLastEnabledAt()
        {
            var (svc, db) = Create();
            await DraftAsync(svc);
            var enabledAt = (await db.Batches.AsNoTracking().SingleAsync(x => x.Id == OnlineBatch)).OnlineLearningEnabledAt;

            var r = await svc.SetBatchOnlineLearningAsync(OnlineBatch, false);
            Assert.True(r.Success, r.Message);
            Assert.Contains("لم يُحذف", r.Message);

            var b = await db.Batches.AsNoTracking().SingleAsync(x => x.Id == OnlineBatch);
            Assert.False(b.IsOnlineLearning);
            Assert.Equal(enabledAt, b.OnlineLearningEnabledAt);
            Assert.Equal(1, await db.LectureContents.CountAsync());
        }

        [Fact]
        public async Task SetBatch_MissingOrDeleted_IsNotFound()
        {
            var (svc, db) = Create();
            db.Batches.Add(new Batch { Id = 40, Name = "محذوفة", IsDeleted = true });
            await db.SaveChangesAsync();
            Assert.True((await svc.SetBatchOnlineLearningAsync(999, true)).NotFound);
            Assert.True((await svc.SetBatchOnlineLearningAsync(40, true)).NotFound);
        }

        [Fact]
        public async Task SetBatch_SameState_IsNoOpSuccess()
        {
            var (svc, _) = Create();
            Assert.True((await svc.SetBatchOnlineLearningAsync(OnlineBatch, true)).Success);
        }

        // ── القراءة ──

        [Fact]
        public async Task GetForLecture_WithoutContent_PrefillsDraftForm()
        {
            var (svc, _) = Create();
            var vm = await svc.GetForLectureAsync(OnlineLecture);
            Assert.NotNull(vm);
            Assert.False(vm!.HasContent);
            Assert.True(vm.BatchIsOnline);
            Assert.Equal("المحاضرة الأولى", vm.CreateInput.Title);
            Assert.Equal("محور الحساب", vm.SectionTitle);
            Assert.Null(await svc.GetForLectureAsync(999));
        }

        [Fact]
        public async Task GetForLecture_WithContent_BuildsServerEmbedAndCountsActiveItems()
        {
            var (svc, db) = Create();
            var id = await DraftAsync(svc);
            await svc.UpdateAsync(id, Update("https://vimeo.com/123456789", duration: "45:00"), Admin);
            await AddItemsAsync(db, id, 2);
            var item = await db.LectureContentItems.FirstAsync();
            item.IsDeleted = true;
            await db.SaveChangesAsync();

            var vm = await svc.GetForLectureAsync(OnlineLecture);
            Assert.True(vm!.HasContent);
            Assert.Equal("https://player.vimeo.com/video/123456789", vm.EmbedPreviewUrl);
            Assert.Equal(1, vm.ActiveItemsCount);
            Assert.Equal("45:00", vm.Input.DurationText);
        }

        [Fact]
        public async Task Statuses_AndSummary_AreScopedToBatch()
        {
            var (svc, db) = Create();
            var id = await DraftAsync(svc);
            await SetStatusAsync(db, id, LectureContentStatus.Published);

            var statuses = await svc.GetStatusesForBatchAsync(OnlineBatch);
            Assert.Equal(LectureContentStatus.Published, statuses[OnlineLecture]);
            Assert.Empty(await svc.GetStatusesForBatchAsync(OfflineBatch));

            var summary = await svc.GetBatchOnlineSummaryAsync(OnlineBatch);
            Assert.Equal(1, summary!.LecturesCount);
            Assert.Equal(1, summary.ContentsCount);
            Assert.Equal(1, summary.PublishedCount);
        }

        [Fact]
        public async Task LectureHasContent_CountsSoftDeletedRows()
        {
            var (svc, db) = Create();
            Assert.False(await svc.LectureHasContentAsync(OnlineLecture));
            db.LectureContents.Add(new LectureContent { LectureId = OnlineLecture, Title = "x", ContentMode = LectureContentMode.FullVideo, IsDeleted = true });
            await db.SaveChangesAsync();
            Assert.True(await svc.LectureHasContentAsync(OnlineLecture));
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S2.3 — الصلاحيات (الكتالوج + السياسات + كل Action محمي + AntiForgery)
    // ═════════════════════════════════════════════════════════════════════
    public class LectureContentPermissionTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QdratNew.csproj"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("QdratNew.csproj غير موجود.");
        }

        [Fact]
        public void Catalog_HasReadCreateEditDelete_AndIsListedInProfileMatrix()
        {
            var keys = AdminControllerActionCatalog.GetActions("LectureContents").Select(a => a.Key).ToList();
            Assert.Equal(new[] { "Read", "Create", "Edit", "Delete", "ManagePartners" }, keys); // OLC-S4.2: ManagePartners (R18)

            var src = File.ReadAllText(Path.Combine(RepoRoot(), "Areas", "Admin", "Controllers", "AdminPermissionProfilesController.cs"));
            Assert.Contains("(\"LectureContents\"", src);
        }

        [Fact]
        public void EveryPolicyConstant_MatchesCatalog_AndIsRegisteredInProgram()
        {
            var program = File.ReadAllText(Path.Combine(RepoRoot(), "Program.cs"));
            var fields = typeof(AdminPermissionPolicies).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.Name.StartsWith("LectureContents_"))
                .ToList();

            Assert.Equal(5, fields.Count); // OLC-S4.2: + ManagePartners
            var catalog = AdminControllerActionCatalog.GetActions("LectureContents").Select(a => "LectureContents:" + a.Key).ToHashSet();
            foreach (var field in fields)
            {
                Assert.Contains($"AdminPermissionPolicies.{field.Name}", program);
                Assert.Contains((string)field.GetValue(null)!, catalog);
            }
            Assert.Contains("ILectureContentAdminService", program);
        }

        [Fact]
        public void LectureContentsController_EveryAction_HasPermission_AndPostsHaveAntiForgery()
        {
            var actions = typeof(LectureContentsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => typeof(IActionResult).IsAssignableFrom(m.ReturnType)
                         || (m.ReturnType.IsGenericType && typeof(IActionResult).IsAssignableFrom(m.ReturnType.GetGenericArguments()[0])))
                .ToList();

            Assert.Equal(new[] { "AddItem", "Batches", "Create", "Edit", "Hide", "Publish", "RefreshItem", "RemoveItem", "Reorder", "Save", "SaveChapters" }, // OLC-S4.2: Batches
                         actions.Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal));
            foreach (var m in actions)
            {
                var attr = Assert.Single(m.GetCustomAttributes<AdminPermissionAttribute>());
                Assert.StartsWith("LectureContents:", attr.Policy);
                if (m.GetCustomAttribute<HttpPostAttribute>() != null)
                    Assert.NotNull(m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            }

            Assert.Equal("LectureContents:Create", typeof(LectureContentsController).GetMethod("Create")!.GetCustomAttribute<AdminPermissionAttribute>()!.Policy);
            Assert.Equal("LectureContents:Edit", typeof(LectureContentsController).GetMethod("Save")!.GetCustomAttribute<AdminPermissionAttribute>()!.Policy);

            // OLC-S3: كل الكتابة POST + AntiForgery؛ الحذف بصلاحية Delete والباقي Edit
            foreach (var name in new[] { "SaveChapters", "AddItem", "Reorder", "RefreshItem", "Publish", "Hide" })
            {
                var m = typeof(LectureContentsController).GetMethod(name)!;
                Assert.NotNull(m.GetCustomAttribute<HttpPostAttribute>());
                Assert.Equal("LectureContents:Edit", m.GetCustomAttribute<AdminPermissionAttribute>()!.Policy);
            }
            var remove = typeof(LectureContentsController).GetMethod("RemoveItem")!;
            Assert.NotNull(remove.GetCustomAttribute<HttpPostAttribute>());
            Assert.Equal("LectureContents:Delete", remove.GetCustomAttribute<AdminPermissionAttribute>()!.Policy);
        }

        [Fact]
        public void BatchesSetOnlineLearning_IsPost_AntiForgery_BatchesEdit()
        {
            var m = typeof(BatchesController).GetMethod("SetOnlineLearning");
            Assert.NotNull(m);
            Assert.NotNull(m!.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.Equal("Batches:Edit", Assert.Single(m.GetCustomAttributes<AdminPermissionAttribute>()).Policy);
        }

        [Fact]
        public void BatchViewModel_OnlineFlag_IsNeverBound_FromEditForm()
        {
            var prop = typeof(QdratNew.ViewModels.Batch.BatchViewModel).GetProperty("IsOnlineLearning");
            Assert.NotNull(prop!.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ModelBinding.BindNeverAttribute>());
        }
    }
}
