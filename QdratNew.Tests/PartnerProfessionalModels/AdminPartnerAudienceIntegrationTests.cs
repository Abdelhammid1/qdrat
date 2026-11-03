using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.ProfessionalModels;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.PartnerProfessionalModels
{
    // PPM-S1 — Program.cs الحقيقي + SQL Server: الصفحات، CSRF، الحفظ بالـ Transaction، وترجمة قاعدة الإتاحة.
    [Collection("SharedIntegrationDb")]
    public class AdminPartnerAudienceIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Marker = "PPMS1";
        private const string OwnerUser = "ppm-s1-owner";

        private readonly CustomWebApplicationFactory _factory;
        private int _partnerA;
        private int _partnerB;

        public AdminPartnerAudienceIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var a = new Partner { Name = $"{Marker} شريك أ", Code = $"{Marker}A", PartnershipStart = DateTime.Today.AddDays(-1), PartnershipEnd = DateTime.Today.AddDays(10) };
            var b = new Partner { Name = $"{Marker} شريك ب", Code = $"{Marker}B", PartnershipStart = DateTime.Today.AddDays(-1), PartnershipEnd = DateTime.Today.AddDays(10) };
            db.Partners.AddRange(a, b);
            await db.SaveChangesAsync();

            db.PartnerSubscriptions.Add(new PartnerSubscription
            {
                PartnerId = a.Id,
                StartDate = DateTime.Today.AddDays(-1),
                EndDate = DateTime.Today.AddDays(10),
                CanUseProfessionalModels = true,
                CanUsePlacementExams = true
            });
            await db.SaveChangesAsync();

            _partnerA = a.Id;
            _partnerB = b.Id;
        }

        public Task DisposeAsync() => CleanupAsync();

        private async Task CleanupAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await db.Database.ExecuteSqlRawAsync("DELETE FROM ProfessionalModelPartners WHERE ProfessionalModelId IN (SELECT Id FROM ProfessionalModels WHERE Title LIKE 'PPMS1%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ProfessionalModels WHERE Title LIKE 'PPMS1%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM PartnerSubscriptions WHERE PartnerId IN (SELECT Id FROM Partners WHERE Code LIKE 'PPMS1%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Partners WHERE Code LIKE 'PPMS1%'");
        }

        private HttpClient OwnerClient()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, OwnerUser);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Owner");
            return client;
        }

        private HttpRequestMessage Post(string url, IEnumerable<KeyValuePair<string, string>> fields)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(OwnerUser);
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("Cookie", cookie);
            request.Headers.Add("RequestVerificationToken", token);
            return request;
        }

        private static List<KeyValuePair<string, string>> ModelFields(string title, PartnerAudience audience, params int[] partners)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                new("Title", title),
                new("Description", "-"),
                new("ModelType", ((int)ProfessionalModelType.Exam).ToString()),
                new("ImportFromCode", ""),
                new("IsForPartners", "true"),
                new("IsForPartners", "false"),
                new("PartnerAudience", ((int)audience).ToString())
            };
            fields.AddRange(partners.Select(p => new KeyValuePair<string, string>("SelectedPartnerIds", p.ToString())));
            return fields;
        }

        private async Task<int> SeedModelAsync(string title, bool forPartners, bool global = false, ProfessionalModelType type = ProfessionalModelType.Exam)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var model = new ProfessionalModel { Title = title, Description = "-", CreatedBy = "-", CreatedAt = DateTime.UtcNow, IsForPartners = forPartners, IsGlobal = global, ModelType = type };
            db.ProfessionalModels.Add(model);
            await db.SaveChangesAsync();
            return model.Id;
        }

        private async Task<string> GetHtmlAsync(string url)
        {
            var response = await OwnerClient().GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Pages_RenderPartnerSection()
        {
            var id = await SeedModelAsync($"{Marker} صفحات", forPartners: true);

            foreach (var url in new[] { "/Admin/ProfessionalModels/Create", $"/Admin/ProfessionalModels/Edit/{id}", $"/Admin/ProfessionalModels/Visibility/{id}" })
            {
                var html = await GetHtmlAsync(url);
                Assert.Contains("موجّه للشركاء", html);
                Assert.Contains($"{Marker} شريك أ", html);
                Assert.Contains("admin-professional-model-partners.js", html);
            }
        }

        [Fact]
        public async Task Create_WithoutAntiForgery_IsRejected()
        {
            var response = await OwnerClient().PostAsync(
                "/Admin/ProfessionalModels/Create",
                new FormUrlEncodedContent(ModelFields($"{Marker} بلا توكن", PartnerAudience.All)));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("Duplicate")]
        [InlineData("AddQuestions")]
        [InlineData("RemoveQuestion")]
        [InlineData("ClassifyUntypedModels")]
        public async Task ModelMutations_WithoutAntiForgery_AreRejected(string action)
        {
            var response = await OwnerClient().PostAsync(
                $"/Admin/ProfessionalModels/{action}",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = "1", ["modelId"] = "1" }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Duplicate_WithToken_Succeeds()
        {
            var id = await SeedModelAsync($"{Marker} للنسخ", forPartners: true, global: true);

            Assert.Contains("duplicateForm", await GetHtmlAsync("/Admin/ProfessionalModels"));

            var response = await OwnerClient().SendAsync(Post("/Admin/ProfessionalModels/Duplicate",
                new[] { new KeyValuePair<string, string>("id", id.ToString()) }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        [Fact]
        public async Task KillSwitch_CaseInsensitiveFalse_HidesModelFromPartner_OnSqlServer()
        {
            var id = await SeedModelAsync($"{Marker} مفتاح", forPartners: true, global: true);
            const string key = ProfessionalModelAvailabilityService.PartnerAccessSettingKey;

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<IProfessionalModelAvailabilityService>();

            var original = await db.SystemSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM SystemSettings WHERE [Key] = {0}", key);

            try
            {
                Assert.True(await service.IsAvailableForPartnerAsync(id, _partnerA, null));

                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO SystemSettings ([Key], [Value], [Description], [CreatedAt]) VALUES ({0}, 'False', '-', GETDATE())", key);
                Assert.False(await service.IsAvailableForPartnerAsync(id, _partnerA, null));
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("DELETE FROM SystemSettings WHERE [Key] = {0}", key);
                if (original != null)
                    await db.Database.ExecuteSqlRawAsync(
                        "INSERT INTO SystemSettings ([Key], [Value], [Description], [CreatedAt]) VALUES ({0}, {1}, '-', GETDATE())", key, original);
            }
        }

        [Fact]
        public async Task Create_Selected_PersistsInSqlServer_AndIndexShowsBadge()
        {
            var title = $"{Marker} محدد";

            var response = await OwnerClient().SendAsync(Post("/Admin/ProfessionalModels/Create", ModelFields(title, PartnerAudience.Selected, _partnerA, _partnerB)));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var model = await db.ProfessionalModels.AsNoTracking().SingleAsync(m => m.Title == title);
            Assert.True(model.IsForPartners);
            Assert.False(model.IsGlobal);
            Assert.Equal(2, await db.ProfessionalModelPartners.CountAsync(p => p.ProfessionalModelId == model.Id));

            Assert.Contains("2 شركاء", await GetHtmlAsync("/Admin/ProfessionalModels"));
        }

        [Fact]
        public async Task Edit_Untyped_ShowsPlaceholder_AndEmptyTypeIsRejectedWithArabicMessage()
        {
            var title = $"{Marker} بلا نوع";
            var id = await SeedModelAsync(title, forPartners: false, type: 0);

            var page = WebUtility.HtmlDecode(await GetHtmlAsync($"/Admin/ProfessionalModels/Edit/{id}"));
            Assert.Contains("اختر النوع", page);

            var fields = new List<KeyValuePair<string, string>>
            {
                new("Title", title),
                new("Description", "-"),
                new("ModelType", "")
            };
            var response = await OwnerClient().SendAsync(Post($"/Admin/ProfessionalModels/Edit/{id}", fields));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("اختر نوع النموذج", html);
            Assert.Contains("اختر النوع", html);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal((ProfessionalModelType)0, (await db.ProfessionalModels.AsNoTracking().SingleAsync(m => m.Id == id)).ModelType);
        }

        [Fact]
        public async Task ClassifyUntyped_BulkPath_OnSqlServer_AndIndexFilterShowsThem()
        {
            var ids = new List<int>();
            for (var i = 0; i < 25; i++)
                ids.Add(await SeedModelAsync($"{Marker} جماعي {i}", forPartners: false, type: 0));
            var classified = await SeedModelAsync($"{Marker} مصنف", forPartners: false, type: ProfessionalModelType.Homework);

            var page = WebUtility.HtmlDecode(await GetHtmlAsync("/Admin/ProfessionalModels/Index?untyped=true"));
            Assert.Contains($"{Marker} جماعي 24", page);
            Assert.Contains("تصنيف النماذج بلا نوع", page);
            Assert.DoesNotContain($"{Marker} مصنف", page);

            var fields = ids.Append(classified)
                .Select(id => new KeyValuePair<string, string>("selectedModelIds", id.ToString()))
                .ToList();
            fields.Add(new("modelType", ((int)ProfessionalModelType.Exam).ToString()));

            var response = await OwnerClient().SendAsync(Post("/Admin/ProfessionalModels/ClassifyUntypedModels", fields));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var models = await db.ProfessionalModels.AsNoTracking()
                .Where(m => m.Title.StartsWith(Marker))
                .Select(m => new { m.Id, m.Title, m.CreatedBy, m.ModelType })
                .ToListAsync();

            Assert.Equal(25, models.Count(m => m.ModelType == ProfessionalModelType.Exam));
            Assert.Equal(ProfessionalModelType.Homework, models.Single(m => m.Id == classified).ModelType);
            Assert.Contains(models, m => m.Title == $"{Marker} جماعي 0");
            Assert.All(models, m => Assert.Equal("-", m.CreatedBy));
        }

        [Fact]
        public async Task Edit_ThenVisibility_SyncAudienceInSqlServer()
        {
            var title = $"{Marker} تعديل";
            var id = await SeedModelAsync(title, forPartners: false);

            var edit = await OwnerClient().SendAsync(Post($"/Admin/ProfessionalModels/Edit/{id}", ModelFields(title, PartnerAudience.Selected, _partnerA)));
            Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);

            var visibilityFields = new List<KeyValuePair<string, string>>
            {
                new("ProfessionalModelId", id.ToString()),
                new("IsForPartners", "true"),
                new("PartnerAudience", ((int)PartnerAudience.All).ToString())
            };
            var visibility = await OwnerClient().SendAsync(Post("/Admin/ProfessionalModels/Visibility", visibilityFields));
            Assert.Equal(HttpStatusCode.Redirect, visibility.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var model = await db.ProfessionalModels.AsNoTracking().SingleAsync(m => m.Id == id);
            Assert.True(model.IsForPartners && model.IsGlobal);
            Assert.Equal(0, await db.ProfessionalModelPartners.CountAsync(p => p.ProfessionalModelId == id));
        }

        [Fact]
        public async Task PlacementLookups_ShowPartnerOnlyExamModelsDirectedToIt_OnSqlServer()
        {
            var exam = await SeedModelAsync($"{Marker} اختبار للكل", forPartners: true, global: true);
            var homework = await SeedModelAsync($"{Marker} واجب للكل", forPartners: true, global: true, type: ProfessionalModelType.Homework);
            var internalExam = await SeedModelAsync($"{Marker} اختبار داخلي", forPartners: false, global: true);

            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<QdratNew.Services.Exams.Placement.IPlacementExamManagementService>();
            var partner = QdratNew.Services.Exams.Placement.PlacementScope.Partner(_partnerA, "u");

            var ids = (await service.GetCreateFromModelAsync(partner)).AvailableModels.Select(m => m.Value).ToList();

            Assert.Contains(exam.ToString(), ids);
            Assert.DoesNotContain(homework.ToString(), ids);
            Assert.DoesNotContain(internalExam.ToString(), ids);
            Assert.NotNull(await service.GetModelSummaryAsync(partner, exam));
            Assert.Null(await service.GetModelSummaryAsync(partner, homework));
        }

        [Fact]
        public async Task Availability_TranslatesOnSqlServer_Internal_All_Selected()
        {
            var internalId = await SeedModelAsync($"{Marker} داخلي", forPartners: false, global: true);
            var allId = await SeedModelAsync($"{Marker} للكل", forPartners: true, global: true);
            var selectedA = await SeedModelAsync($"{Marker} لشريك أ", forPartners: true);
            var selectedB = await SeedModelAsync($"{Marker} لشريك ب", forPartners: true);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ProfessionalModelPartners.AddRange(
                new ProfessionalModelPartner { ProfessionalModelId = selectedA, PartnerId = _partnerA },
                new ProfessionalModelPartner { ProfessionalModelId = selectedB, PartnerId = _partnerB });
            await db.SaveChangesAsync();

            var service = scope.ServiceProvider.GetRequiredService<IProfessionalModelAvailabilityService>();
            var ids = await service.AvailableForPartner(_partnerA, null)
                .Where(m => m.Title.StartsWith(Marker))
                .Select(m => m.Id)
                .ToListAsync();

            Assert.Contains(allId, ids);
            Assert.Contains(selectedA, ids);
            Assert.DoesNotContain(internalId, ids);
            Assert.DoesNotContain(selectedB, ids);

            // شريك بلا عقد: لا يرى حتى النموذج العام
            Assert.False(await service.IsAvailableForPartnerAsync(allId, _partnerB, null));
        }
    }
}
