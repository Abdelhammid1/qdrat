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
    /// OLC-S4 عبر HTTP على قاعدة الاختبار: R16 (رفض تفعيل دفعة شريك بلا البند)، R18 (ManagePartners على كل مسار يمس
    /// دفعة شريك + «الدفعات الأونلاين» بجمهور الشركاء)، R19 (صفحة الشريك للقراءة ضمن نطاقه)، وتنبيه عقد الشريك (S4.4).
    /// يعيد استخدام بيانات APD (A: عقد نشط بفترة، B: عقد نشط) ويضيف محاضرة لكل من A1 وB1.
    /// </summary>
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class PartnerOnlineLearningIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string OwnerUser = "apds1-olcs4-owner";
        private const string LimitedUser = "apds1-olcs4-limited";
        private const string PartnerAUser = "apds1-olcs4-partner-a";
        private const string FeatureKey = "OnlineLectureContent.Enabled";

        private readonly CustomWebApplicationFactory _factory;
        private ApdSeed _seed = null!;
        private int _lectureA, _lectureB, _contentA, _contractA;
        private string? _featureBefore;

        public PartnerOnlineLearningIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await CleanupOlcAsync(db);
            await ApdTestData.CleanupAsync(db);
            _seed = await ApdTestData.SeedAsync(db);

            // A: البند مفعّل، B: عقد نشط بلا البند (R16)
            var contracts = await db.PartnerSubscriptions.Where(s => s.PartnerId == _seed.PartnerA || s.PartnerId == _seed.PartnerB).ToListAsync();
            contracts.Single(c => c.PartnerId == _seed.PartnerA).CanAccessEducationalContent = true;
            _contractA = contracts.Single(c => c.PartnerId == _seed.PartnerA).Id;

            var anchor = await db.Lecture.AsNoTracking().Select(l => new { l.InstructorId }).FirstAsync();
            var sectionId = await db.Lessons.AsNoTracking().Where(l => l.Id == _seed.LessonId).Select(l => l.SectionId).FirstAsync();
            Lecture NewLecture(int batchId) => new()
            {
                Title = $"{ApdTestData.Marker} محاضرة أونلاين", Location = "-", Date = DateTime.Today, InstructorId = anchor.InstructorId,
                SectionId = sectionId, CourseId = _seed.CourseId, BatchId = batchId
            };
            var la = NewLecture(_seed.BatchA1);
            var lb = NewLecture(_seed.BatchB1);
            db.Lecture.AddRange(la, lb);

            db.Users.AddRange(ApdTestData.NewUser(OwnerUser), ApdTestData.NewUser(LimitedUser), ApdTestData.NewUser(PartnerAUser));
            db.UserPartners.Add(new UserPartner { UserId = PartnerAUser, PartnerId = _seed.PartnerA });

            var profile = new AdminProfile { Name = $"{ApdTestData.Marker} OLCS4 بلا ManagePartners" };
            db.AdminProfiles.Add(profile);
            await db.SaveChangesAsync();
            _lectureA = la.Id;
            _lectureB = lb.Id;

            // كل صلاحيات المحتوى والدفعات والمحاضرات — عدا ManagePartners
            foreach (var (controller, actions) in new[]
            {
                ("LectureContents", new[] { "Read", "Create", "Edit", "Delete" }),
                ("Batches", new[] { "Read", "Details", "Edit" }),
                ("Lectures", new[] { "Read" })
            })
            {
                var row = new AdminProfileControllerPermission
                {
                    AdminProfileId = profile.Id, ControllerName = controller, AccessLevel = AdminControllerAccessLevel.Read, HasCustomPermissions = true
                };
                foreach (var action in actions)
                    row.CustomPermissions.Add(new AdminProfileCustomPermission { ActionName = action, IsAllowed = true });
                db.AdminProfileControllerPermissions.Add(row);
            }
            db.AdminUserProfiles.Add(new AdminUserProfile { UserId = LimitedUser, AdminProfileId = profile.Id });

            // الميزة مفعّلة لصفحة الشريك (تُعاد كما كانت في DisposeAsync)
            var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == FeatureKey);
            _featureBefore = setting?.Value;
            if (setting == null)
                db.SystemSettings.Add(new SystemSetting { Key = FeatureKey, Value = "true" });
            else
                setting.Value = "true";

            await db.SaveChangesAsync();
        }

        public async Task DisposeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await CleanupOlcAsync(db);
            await ApdTestData.CleanupAsync(db);

            if (_featureBefore == null)
                await db.SystemSettings.Where(s => s.Key == FeatureKey).ExecuteDeleteAsync();
            else
                await db.SystemSettings.Where(s => s.Key == FeatureKey).ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, _featureBefore));
        }

        private static async Task CleanupOlcAsync(ApplicationDbContext db)
        {
            const string lectures = "(SELECT Id FROM [Lecture] WHERE Title LIKE 'APDS1%')";
            const string contents = "(SELECT Id FROM LectureContents WHERE LectureId IN " + lectures + ")";
            foreach (var sql in new[]
            {
                $"DELETE FROM LectureContentItemProgress WHERE LectureContentItemId IN (SELECT Id FROM LectureContentItems WHERE LectureContentId IN {contents})",
                $"DELETE FROM LectureContentProgress WHERE LectureContentId IN {contents}",
                $"DELETE FROM LectureContentItems WHERE LectureContentId IN {contents}",
                $"DELETE FROM LectureContents WHERE LectureId IN {lectures}",
                "DELETE FROM [Lecture] WHERE Title LIKE 'APDS1%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        // ───────────────────────── أدوات HTTP ─────────────────────────

        private HttpClient Client(string userId, string role)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
            return client;
        }

        private async Task<HttpResponseMessage> PostAsync(HttpClient client, string userId, string url, Dictionary<string, string> fields, string? cookies = null)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(userId);
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("Cookie", string.IsNullOrEmpty(cookies) ? cookie : $"{cookie}; {cookies}");
            request.Headers.Add("RequestVerificationToken", token);
            return await client.SendAsync(request);
        }

        private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string? cookies = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(cookies))
                request.Headers.Add("Cookie", cookies);
            return await client.SendAsync(request);
        }

        private static async Task<string> HtmlAsync(HttpResponseMessage response)
            => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        private async Task<string> OkHtmlAsync(HttpClient client, string url, string? cookies = null)
        {
            var response = await GetAsync(client, url, cookies);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} ⇒ {(int)response.StatusCode}");
            return await HtmlAsync(response);
        }

        private static string CookiesOf(HttpResponseMessage response)
            => response.Headers.TryGetValues("Set-Cookie", out var values)
                ? string.Join("; ", values.Select(v => v.Split(';')[0]))
                : "";

        private async Task<T> DbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private Task<bool> IsOnlineAsync(int batchId)
            => DbAsync(db => db.Batches.Where(b => b.Id == batchId).Select(b => b.IsOnlineLearning).SingleAsync());

        private Dictionary<string, string> Toggle(int batchId, bool enable)
            => new() { ["id"] = batchId.ToString(), ["enable"] = enable ? "true" : "false", ["returnTo"] = "Details" };

        // ───────────────────────── R16 + R18 (الأدمن) ─────────────────────────

        [Fact]
        public async Task Admin_R16_ClauseGatesActivation_And_R18_ManagePartnersGuardsEveryPartnerPath()
        {
            var owner = Client(OwnerUser, "Owner");
            var limited = Client(LimitedUser, "Admin");

            // R16: B بلا البند ⇒ يُرفض التفعيل برسالة في البطاقة
            var refused = await PostAsync(owner, OwnerUser, "/Admin/Batches/SetOnlineLearning", Toggle(_seed.BatchB1, true));
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.False(await IsOnlineAsync(_seed.BatchB1));
            var detailsB = await OkHtmlAsync(owner, $"/Admin/Batches/Details/{_seed.BatchB1}");
            Assert.Contains("لا يشمل بند «المحتوى التعليمي»", detailsB);
            Assert.Contains("غير مفعّل في العقد", detailsB);

            // R16: A بالبند ⇒ يُفعَّل
            Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(owner, OwnerUser, "/Admin/Batches/SetOnlineLearning", Toggle(_seed.BatchA1, true))).StatusCode);
            Assert.True(await IsOnlineAsync(_seed.BatchA1));
            Assert.Contains("البند مفعّل", await OkHtmlAsync(owner, $"/Admin/Batches/Details/{_seed.BatchA1}"));

            // R18: بلا ManagePartners — لا تفعيل/إلغاء لدفعة شريك، ولا صفحة محتوى محاضرتها، ولا إنشاء
            Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(limited, LimitedUser, "/Admin/Batches/SetOnlineLearning", Toggle(_seed.BatchA1, false))).StatusCode);
            Assert.True(await IsOnlineAsync(_seed.BatchA1));
            Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(limited, $"/Admin/LectureContents/Edit?lectureId={_lectureA}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(limited, LimitedUser, $"/Admin/LectureContents/Create?lectureId={_lectureA}",
                new() { ["CreateInput.Title"] = "محاولة", ["CreateInput.ContentMode"] = "1" })).StatusCode);
            Assert.False(await DbAsync(db => db.LectureContents.IgnoreQueryFilters().AnyAsync(c => c.LectureId == _lectureA)));

            // بطاقة التفعيل تُعرض للقراءة فقط، ورابط المحتوى لا يظهر في قائمة محاضرات دفعة الشريك
            Assert.DoesNotContain("إلغاء التعليم أونلاين</button>", await OkHtmlAsync(limited, $"/Admin/Batches/Details/{_seed.BatchA1}"));
            Assert.DoesNotContain($"/Admin/LectureContents/Edit?lectureId={_lectureA}", await OkHtmlAsync(limited, $"/Admin/Lectures/Lectures?batchId={_seed.BatchA1}"));

            // المالك: الصفحة متاحة والإنشاء يعمل
            Assert.Contains("المحتوى التعليمي أونلاين", await OkHtmlAsync(owner, $"/Admin/LectureContents/Edit?lectureId={_lectureA}"));
            Assert.Contains($"/Admin/LectureContents/Edit?lectureId={_lectureA}", await OkHtmlAsync(owner, $"/Admin/Lectures/Lectures?batchId={_seed.BatchA1}"));
            var created = await PostAsync(owner, OwnerUser, $"/Admin/LectureContents/Create?lectureId={_lectureA}",
                new() { ["CreateInput.Title"] = "محتوى A", ["CreateInput.ContentMode"] = "1" });
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            Assert.True(await DbAsync(db => db.LectureContents.AnyAsync(c => c.LectureId == _lectureA)));
        }

        [Fact]
        public async Task Admin_OnlineBatchesScreen_AudiencePlatformPartners_WithClauseStates()
        {
            var owner = Client(OwnerUser, "Owner");
            var limited = Client(LimitedUser, "Admin");
            await DbAsync(db => db.Batches.Where(b => b.Id == _seed.BatchA1 || b.Id == _seed.BatchI1)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsOnlineLearning, true)));

            var batchA1 = $"{ApdTestData.Marker} دفعة A1";
            var batchB1 = $"{ApdTestData.Marker} دفعة B1";
            var batchI1 = $"{ApdTestData.Marker} دفعة معهد 1";

            // المنصة: دفعات المعهد فقط، وبلا ManagePartners لا يظهر خيار «المدارس الشريكة» والطلب المصطنع 403
            var platform = await OkHtmlAsync(limited, "/Admin/LectureContents/Batches");
            Assert.Contains(batchI1, platform);
            Assert.DoesNotContain(batchA1, platform);
            Assert.DoesNotContain($"name=\"Audience\" value=\"{(int)DispatchAudience.Partners}\"", platform);
            Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(limited, "/Admin/LectureContents/Batches?Audience=2")).StatusCode);

            // الشركاء (كلهم): A1 الأونلاين بحالة البند، وحالة كل شريك
            var partners = await OkHtmlAsync(owner, "/Admin/LectureContents/Batches?Audience=2");
            Assert.Contains(batchA1, partners);
            Assert.DoesNotContain(batchI1, partners);
            Assert.Contains("البند مفعّل", partners);
            Assert.Contains("غير مفعّل في العقد", partners);

            // اختيار شريك بعينه (EF.Constant داخل IN على SQL Server) + الدفعات غير المفعّلة
            var onlyB = await OkHtmlAsync(owner, $"/Admin/LectureContents/Batches?Audience=2&PartnerIds={_seed.PartnerB}&IncludeInactive=true");
            Assert.Contains(batchB1, onlyB);
            Assert.DoesNotContain(batchA1, onlyB);

            // معرّف شريك غير موجود يُتجاهل (لا يُوثق بالطلب) ⇒ كل الشركاء
            Assert.Contains(batchA1, await OkHtmlAsync(owner, "/Admin/LectureContents/Batches?Audience=2&PartnerIds=987654321"));
        }

        // ───────────────────────── S4.4 — عقد الشريك ─────────────────────────

        [Fact]
        public async Task PartnerContractEdit_DescribesClause_AndWarnsWhenOnlineBatchesExist()
        {
            var owner = Client(OwnerUser, "Owner");
            var before = await OkHtmlAsync(owner, $"/Admin/PartnerSubscriptions/Edit/{_contractA}");
            Assert.Contains("يتيح المحتوى التعليمي أونلاين لدفعات الشريك", before);
            Assert.DoesNotContain("role=\"alert\" data-olc-clause-warning", before);

            await DbAsync(db => db.Batches.Where(b => b.Id == _seed.BatchA1).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsOnlineLearning, true)));
            var after = await OkHtmlAsync(owner, $"/Admin/PartnerSubscriptions/Edit/{_contractA}");
            Assert.Contains("role=\"alert\" data-olc-clause-warning", after);
            Assert.Contains("للشريك 1 دفعة مفعّل لها التعليم أونلاين", after);
        }

        // ───────────────────────── R19 — منطقة الشريك ─────────────────────────

        [Fact]
        public async Task Partner_ReadOnlyPages_ScopedToOwnBatches_AndGuardedByClause()
        {
            await DbAsync(async db =>
            {
                await db.Batches.Where(b => b.Id == _seed.BatchA1 || b.Id == _seed.BatchB1)
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsOnlineLearning, true));
                var a = new LectureContent { LectureId = _lectureA, Title = "محتوى منشور A", ContentMode = LectureContentMode.FullVideo, Status = LectureContentStatus.Published, DurationSeconds = 600, CreatedAt = DateTime.UtcNow };
                var b = new LectureContent { LectureId = _lectureB, Title = "محتوى B", ContentMode = LectureContentMode.FullVideo, Status = LectureContentStatus.Published, CreatedAt = DateTime.UtcNow };
                db.LectureContents.AddRange(a, b);
                await db.SaveChangesAsync();
                db.LectureContentProgress.Add(new LectureContentProgress { LectureContentId = a.Id, StudentId = _seed.ActiveStudentA, WatchedSeconds = 600, IsCompleted = true, CreatedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
                _contentA = a.Id;
                _lectureB = b.Id; // معرّف محتوى B
                return true;
            });

            var client = Client(PartnerAUser, "Partner");
            var selected = await PostAsync(client, PartnerAUser, "/Partner/PartnerContext/Set", new() { ["partnerId"] = _seed.PartnerA.ToString() });
            var cookies = CookiesOf(selected);

            var index = await OkHtmlAsync(client, "/Partner/LearningContent", cookies);
            Assert.Contains($"{ApdTestData.Marker} دفعة A1", index);
            Assert.DoesNotContain($"{ApdTestData.Marker} دفعة B1", index);

            var batch = await OkHtmlAsync(client, $"/Partner/LearningContent/Batch/{_seed.BatchA1}", cookies);
            Assert.Contains("محتوى منشور A", batch);
            Assert.DoesNotContain("vimeo", batch, StringComparison.OrdinalIgnoreCase);

            var lecture = await OkHtmlAsync(client, $"/Partner/LearningContent/Lecture/{_contentA}", cookies);
            Assert.Contains($"طالب {ApdTestData.Marker} 1", lecture);
            Assert.Contains("أتمّ", lecture);

            // نطاق الشريك: دفعة ومحتوى شريك آخر ⇒ 404 بلا تمييز
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(client, $"/Partner/LearningContent/Batch/{_seed.BatchB1}", cookies)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(client, $"/Partner/LearningContent/Lecture/{_lectureB}", cookies)).StatusCode);

            // الشريط الجانبي يعرض الرابط الجديد
            Assert.Contains("/Partner/LearningContent", index);

            // إلغاء البند في عقد نشط ⇒ الصفحة محجوبة (FeatureNotAllowed 403)
            await DbAsync(db => db.PartnerSubscriptions.Where(s => s.Id == _contractA)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CanAccessEducationalContent, false)));
            Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(client, "/Partner/LearningContent", cookies)).StatusCode);
        }
    }
}
