using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Tests.AdminPartnerDispatch;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.OnlineLecture
{
    /// <summary>
    /// OLC-S2 عبر HTTP على قاعدة الاختبار المعزولة: تفعيل الدفعة، الشارات والروابط، إنشاء المسودة، الحفظ بتحقق R8،
    /// الصلاحية، وحارس حذف المحاضرة. يستخدم محاضرة موجودة في نسخة الاختبار ويعيد حالة دفعتها كما كانت.
    /// </summary>
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class AdminLectureContentIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string OwnerUser = "olcs2-owner";
        private const string NoPermUser = "olcs2-no-perm";
        private const string ProfileName = "OLCS2 بلا صلاحية المحتوى";

        private readonly CustomWebApplicationFactory _factory;
        private int _lectureId;
        private int _batchId;
        private bool _batchWasOnline;
        private DateTime? _batchEnabledAt;

        public AdminLectureContentIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await CleanupUsersAsync(db);

            var lecture = await db.Lecture.AsNoTracking()
                .Where(l => !l.Batch.IsDeleted && !l.Batch.IsArchived
                         && !db.LectureContents.IgnoreQueryFilters().Any(c => c.LectureId == l.Id))
                .OrderBy(l => l.Id)
                .Select(l => new { l.Id, l.BatchId, l.Batch.IsOnlineLearning, l.Batch.OnlineLearningEnabledAt })
                .FirstOrDefaultAsync();
            Assert.True(lecture != null, "لا توجد محاضرة صالحة في قاعدة الاختبار.");

            _lectureId = lecture!.Id;
            _batchId = lecture.BatchId;
            _batchWasOnline = lecture.IsOnlineLearning;
            _batchEnabledAt = lecture.OnlineLearningEnabledAt;

            await db.Batches.Where(b => b.Id == _batchId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsOnlineLearning, false));

            db.Users.AddRange(ApdTestData.NewUser(OwnerUser), ApdTestData.NewUser(NoPermUser));
            var profile = new AdminProfile { Name = ProfileName };
            db.AdminProfiles.Add(profile);
            await db.SaveChangesAsync();
            db.AdminProfileControllerPermissions.Add(new AdminProfileControllerPermission
            {
                AdminProfileId = profile.Id,
                ControllerName = "Lectures",
                AccessLevel = AdminControllerAccessLevel.Read
            });
            db.AdminUserProfiles.Add(new AdminUserProfile { UserId = NoPermUser, AdminProfileId = profile.Id });
            await db.SaveChangesAsync();
        }

        public async Task DisposeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE i FROM LectureContentItems i JOIN LectureContents c ON c.Id = i.LectureContentId WHERE c.LectureId = {_lectureId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM LectureContents WHERE LectureId = {_lectureId}");
            await db.Batches.Where(b => b.Id == _batchId).ExecuteUpdateAsync(s => s
                .SetProperty(b => b.IsOnlineLearning, _batchWasOnline)
                .SetProperty(b => b.OnlineLearningEnabledAt, _batchEnabledAt));
            await CleanupUsersAsync(db);
        }

        private static async Task CleanupUsersAsync(ApplicationDbContext db)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM AdminUserProfiles WHERE UserId IN ({OwnerUser}, {NoPermUser})");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM AdminProfileControllerPermissions WHERE AdminProfileId IN (SELECT Id FROM AdminProfiles WHERE Name = {ProfileName})");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM AdminProfiles WHERE Name = {ProfileName}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM AspNetUsers WHERE Id IN ({OwnerUser}, {NoPermUser})");
        }

        private HttpClient Client(string userId, string role)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
            return client;
        }

        private async Task<HttpResponseMessage> PostAsync(string userId, string role, string url, Dictionary<string, string> fields)
        {
            var client = Client(userId, role);
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(userId);
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("Cookie", cookie);
            request.Headers.Add("RequestVerificationToken", token);
            return await client.SendAsync(request);
        }

        private async Task<string> GetHtmlAsync(string url, string userId = OwnerUser, string role = "Owner")
        {
            var response = await Client(userId, role).GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        }

        private async Task<T> DbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        [Fact]
        public async Task FullAdminJourney_EnableBatch_CreateDraft_SaveVideo_Disable()
        {
            // قبل التفعيل: R1 يمنع الإنشاء، والصفحة تشرح السبب
            var before = await GetHtmlAsync($"/Admin/LectureContents/Edit?lectureId={_lectureId}");
            Assert.Contains("غير مفعّل لها التعليم أونلاين", before);
            Assert.DoesNotContain("إنشاء مسودة المحتوى", before);

            var refused = await PostAsync(OwnerUser, "Owner", $"/Admin/LectureContents/Create?lectureId={_lectureId}",
                new() { ["CreateInput.Title"] = "محتوى", ["CreateInput.ContentMode"] = "1" });
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.False(await DbAsync(db => db.LectureContents.IgnoreQueryFilters().AnyAsync(c => c.LectureId == _lectureId)));

            // تفعيل الدفعة من شاشة التعديل (S2.1)
            var batchEdit = await GetHtmlAsync($"/Admin/Batches/Edit/{_batchId}");
            Assert.Contains("تفعيل التعليم أونلاين", batchEdit);

            var enable = await PostAsync(OwnerUser, "Owner", "/Admin/Batches/SetOnlineLearning",
                new() { ["id"] = _batchId.ToString(), ["enable"] = "true", ["returnTo"] = "Details" });
            Assert.Equal(HttpStatusCode.Redirect, enable.StatusCode);
            Assert.Contains($"/Admin/Batches/Details/{_batchId}", enable.Headers.Location!.OriginalString);
            Assert.True(await DbAsync(db => db.Batches.Where(b => b.Id == _batchId).Select(b => b.IsOnlineLearning).SingleAsync()));

            Assert.Contains("إلغاء التعليم أونلاين", await GetHtmlAsync($"/Admin/Batches/Details/{_batchId}"));
            Assert.Contains("batch-online-badge", await GetHtmlAsync("/Admin/Batches?view=all"));

            // الرابط من قائمة/تفاصيل المحاضرة (S2.3)
            Assert.Contains($"/Admin/LectureContents/Edit?lectureId={_lectureId}", await GetHtmlAsync($"/Admin/Lectures/Lectures?batchId={_batchId}"));
            Assert.Contains("المحتوى التعليمي أونلاين", await GetHtmlAsync($"/Admin/Lectures/Details/{_lectureId}"));

            // إنشاء المسودة
            var create = await PostAsync(OwnerUser, "Owner", $"/Admin/LectureContents/Create?lectureId={_lectureId}",
                new() { ["CreateInput.Title"] = "OLCS2 محتوى المحاضرة", ["CreateInput.ContentMode"] = "1" });
            Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
            var content = await DbAsync(db => db.LectureContents.AsNoTracking().SingleAsync(c => c.LectureId == _lectureId));
            Assert.Equal(LectureContentStatus.Draft, content.Status);
            Assert.Equal(OwnerUser, content.CreatedBy);

            var editPage = await GetHtmlAsync($"/Admin/LectureContents/Edit?lectureId={_lectureId}");
            Assert.Contains("البيانات والوضع", editPage);

            // R8: رابط غير آمن يُرفض ويُعاد عرض الصفحة بالخطأ
            var bad = await PostAsync(OwnerUser, "Owner", $"/Admin/LectureContents/Save/{content.Id}", new()
            {
                ["Input.Title"] = "OLCS2 محتوى المحاضرة",
                ["Input.ContentMode"] = "1",
                ["Input.VideoUrl"] = "https://evil.example/x.mp4"
            });
            Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
            Assert.Contains("YouTube أو Vimeo", WebUtility.HtmlDecode(await bad.Content.ReadAsStringAsync()));

            var good = await PostAsync(OwnerUser, "Owner", $"/Admin/LectureContents/Save/{content.Id}", new()
            {
                ["Input.Title"] = "OLCS2 محتوى المحاضرة",
                ["Input.ContentMode"] = "1",
                ["Input.VideoUrl"] = "https://vimeo.com/123456789",
                ["Input.DurationText"] = "45:00"
            });
            Assert.Equal(HttpStatusCode.Redirect, good.StatusCode);
            var saved = await DbAsync(db => db.LectureContents.AsNoTracking().SingleAsync(c => c.Id == content.Id));
            Assert.Equal("123456789", saved.VideoExternalId);
            Assert.Equal(2700, saved.DurationSeconds);
            Assert.Contains("https://player.vimeo.com/video/123456789", await GetHtmlAsync($"/Admin/LectureContents/Edit?lectureId={_lectureId}"));

            // حارس حذف المحاضرة: لها محتوى ⇒ لا حذف
            var delete = await PostAsync(OwnerUser, "Owner", $"/Admin/Lectures/Delete/{_lectureId}", new());
            Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);
            Assert.True(await DbAsync(db => db.Lecture.AnyAsync(l => l.Id == _lectureId)));

            // الإلغاء لا يحذف المحتوى
            var disable = await PostAsync(OwnerUser, "Owner", "/Admin/Batches/SetOnlineLearning",
                new() { ["id"] = _batchId.ToString(), ["enable"] = "false" });
            Assert.Equal(HttpStatusCode.Redirect, disable.StatusCode);
            Assert.False(await DbAsync(db => db.Batches.Where(b => b.Id == _batchId).Select(b => b.IsOnlineLearning).SingleAsync()));
            Assert.True(await DbAsync(db => db.LectureContents.AnyAsync(c => c.Id == content.Id)));
        }

        [Fact]
        public async Task S3Journey_Chapters_Publish_Hide_OverHttp()
        {
            await DbAsync(db => db.Batches.Where(b => b.Id == _batchId).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsOnlineLearning, true)));

            var create = await PostAsync(OwnerUser, "Owner", $"/Admin/LectureContents/Create?lectureId={_lectureId}",
                new() { ["CreateInput.Title"] = "OLCS3 محتوى", ["CreateInput.ContentMode"] = "1" });
            Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
            var contentId = await DbAsync(db => db.LectureContents.Where(c => c.LectureId == _lectureId).Select(c => c.Id).SingleAsync());

            // النشر بلا رابط مرفوض (R4) ويبقى مسودة
            var early = await PostAsync(OwnerUser, "Owner", $"/Admin/LectureContents/Publish/{contentId}", new());
            Assert.Equal(HttpStatusCode.Redirect, early.StatusCode);
            Assert.Equal(LectureContentStatus.Draft, await DbAsync(db => db.LectureContents.Where(c => c.Id == contentId).Select(c => c.Status).SingleAsync()));

            var save = await PostAsync(OwnerUser, "Owner", $"/Admin/LectureContents/Save/{contentId}", new()
            {
                ["Input.Title"] = "OLCS3 محتوى",
                ["Input.ContentMode"] = "1",
                ["Input.VideoUrl"] = "https://youtu.be/dQw4w9WgXcQ",
                ["Input.DurationText"] = "30:00"
            });
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

            // محرر الفصول: أول مؤشر نشط من محور المحاضرة (إن وُجد في نسخة الاختبار)
            var lessonId = await DbAsync(db => db.Lessons.Where(l => l.IsActive && l.SectionId == db.Lecture.Where(x => x.Id == _lectureId).Select(x => x.SectionId).First())
                .OrderBy(l => l.Id).Select(l => (int?)l.Id).FirstOrDefaultAsync());
            var editPage = await GetHtmlAsync($"/Admin/LectureContents/Edit?lectureId={_lectureId}");
            Assert.Contains("النشر للطلاب", editPage);
            if (lessonId is { } lid)
            {
                Assert.Contains("حفظ الفصول", editPage);

                var overlapping = await PostAsync(OwnerUser, "Owner", $"/Admin/LectureContents/SaveChapters/{contentId}", new()
                {
                    ["Chapters[0].LessonId"] = lid.ToString(),
                    ["Chapters[0].Include"] = "true",
                    ["Chapters[0].StartText"] = "10:00",
                    ["Chapters[0].EndText"] = "40:00"
                });
                Assert.Equal(HttpStatusCode.OK, overlapping.StatusCode);   // يُعاد عرض الصفحة بالخطأ
                Assert.Contains("تتجاوز مدة الفيديو", WebUtility.HtmlDecode(await overlapping.Content.ReadAsStringAsync()));

                var chapters = await PostAsync(OwnerUser, "Owner", $"/Admin/LectureContents/SaveChapters/{contentId}", new()
                {
                    ["Chapters[0].LessonId"] = lid.ToString(),
                    ["Chapters[0].Include"] = "true",
                    ["Chapters[0].StartText"] = "0:00",
                    ["Chapters[0].EndText"] = "12:30"
                });
                Assert.Equal(HttpStatusCode.Redirect, chapters.StatusCode);
                var item = await DbAsync(db => db.LectureContentItems.AsNoTracking().SingleAsync(i => i.LectureContentId == contentId));
                Assert.Equal((lid, 0, 750), (item.LessonId, item.StartSeconds!.Value, item.EndSeconds!.Value));
            }

            // بلا صلاحية ⇒ 403، وبلا AntiForgery ⇒ 400
            Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(NoPermUser, "Admin", $"/Admin/LectureContents/Publish/{contentId}", new())).StatusCode);
            var noToken = await Client(OwnerUser, "Owner").PostAsync($"/Admin/LectureContents/Publish/{contentId}", new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

            var publish = await PostAsync(OwnerUser, "Owner", $"/Admin/LectureContents/Publish/{contentId}", new());
            Assert.Equal(HttpStatusCode.Redirect, publish.StatusCode);
            var published = await DbAsync(db => db.LectureContents.AsNoTracking().SingleAsync(c => c.Id == contentId));
            Assert.Equal(LectureContentStatus.Published, published.Status);
            Assert.Equal(OwnerUser, published.PublishedByUserId);
            Assert.NotNull(published.PublishedAt);
            Assert.Contains("إخفاء عن الطلاب", await GetHtmlAsync($"/Admin/LectureContents/Edit?lectureId={_lectureId}"));

            var hide = await PostAsync(OwnerUser, "Owner", $"/Admin/LectureContents/Hide/{contentId}", new());
            Assert.Equal(HttpStatusCode.Redirect, hide.StatusCode);
            Assert.Equal(LectureContentStatus.Hidden, await DbAsync(db => db.LectureContents.Where(c => c.Id == contentId).Select(c => c.Status).SingleAsync()));
            Assert.Contains("إعادة النشر", await GetHtmlAsync($"/Admin/LectureContents/Edit?lectureId={_lectureId}"));
        }

        [Fact]
        public async Task S7_Report_And_BatchKpis_OverHttp()
        {
            await DbAsync(db => db.Batches.Where(b => b.Id == _batchId).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsOnlineLearning, true)));
            await DbAsync(async db =>
            {
                db.LectureContents.Add(new LectureContent
                {
                    LectureId = _lectureId, Title = "OLCS7 تقرير", ContentMode = LectureContentMode.FullVideo, Status = LectureContentStatus.Published,
                    VideoProvider = LectureVideoProvider.YouTube, VideoUrl = "https://youtu.be/dQw4w9WgXcQ", VideoExternalId = "dQw4w9WgXcQ",
                    DurationSeconds = 600, PublishedAt = DateTime.UtcNow, PublishedByUserId = OwnerUser, CreatedAt = DateTime.UtcNow
                });
                return await db.SaveChangesAsync();
            });

            var report = await GetHtmlAsync($"/Admin/LectureContents/Report?lectureId={_lectureId}");
            Assert.Contains("تقرير المشاهدة", report);
            Assert.Contains("OLCS7 تقرير", report);
            Assert.Contains("أتمّوا المحاضرة", report);
            Assert.Contains("متوقفون عند القفل", report);

            Assert.Contains($"/Admin/LectureContents/Report?lectureId={_lectureId}", await GetHtmlAsync($"/Admin/LectureContents/Edit?lectureId={_lectureId}"));

            // S7.4: KPIs في بطاقة الدفعة
            var details = await GetHtmlAsync($"/Admin/Batches/Details/{_batchId}");
            Assert.Contains("المحاضرات المنشورة", details);
            Assert.Contains("متوسط زمن النشر بعد المحاضرة", details);
            Assert.Contains("نسبة الإتمام لكل منهج", details);

            // بلا صلاحية ⇒ 403، ومحاضرة بلا محتوى ⇒ 404، والطالب لا يفتحها
            Assert.Equal(HttpStatusCode.Forbidden, (await Client(NoPermUser, "Admin").GetAsync($"/Admin/LectureContents/Report?lectureId={_lectureId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client(OwnerUser, "Owner").GetAsync("/Admin/LectureContents/Report?lectureId=0")).StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, (await Client(OwnerUser, "Student").GetAsync($"/Admin/LectureContents/Report?lectureId={_lectureId}")).StatusCode);
        }

        [Fact]
        public async Task WithoutLectureContentsPermission_IsForbidden()
        {
            var get = await Client(NoPermUser, "Admin").GetAsync($"/Admin/LectureContents/Edit?lectureId={_lectureId}");
            Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);

            var post = await PostAsync(NoPermUser, "Admin", $"/Admin/LectureContents/Create?lectureId={_lectureId}",
                new() { ["CreateInput.Title"] = "x", ["CreateInput.ContentMode"] = "1" });
            Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        }

        [Fact]
        public async Task Post_WithoutAntiForgery_IsRejected()
        {
            var response = await Client(OwnerUser, "Owner").PostAsync("/Admin/Batches/SetOnlineLearning",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = _batchId.ToString(), ["enable"] = "true" }));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await DbAsync(db => db.Batches.Where(b => b.Id == _batchId).Select(b => b.IsOnlineLearning).SingleAsync()));
        }

        [Fact]
        public async Task Student_CannotOpenAdminContentPage()
        {
            var response = await Client(OwnerUser, "Student").GetAsync($"/Admin/LectureContents/Edit?lectureId={_lectureId}");
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
