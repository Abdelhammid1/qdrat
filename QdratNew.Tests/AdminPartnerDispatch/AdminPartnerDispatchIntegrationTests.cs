using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.PartnerDispatch;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S1.4 — يضيف للتطبيق الحقيقي Controller اختبار يعرض _DispatchAudiencePicker كما ستعرضه الشاشات الخمس،
    // ويعيد AudienceSelection بعد الـ Model Binding للتأكد من أسماء الحقول.
    public sealed class ApdWebApplicationFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
                services.AddControllersWithViews().AddApplicationPart(typeof(ApdPickerTestController).Assembly));
        }
    }

    [Route("__apd-test")]
    [Authorize(Policy = "AdminArea")]
    public sealed class ApdPickerTestController : Controller
    {
        [HttpGet("picker")]
        public async Task<IActionResult> Picker(
            [FromServices] IDispatchAudiencePickerBuilder builder,
            int modelId, int? audience, int? partnerId, int? batchId, bool includePartners = false)
        {
            var selection = new AudienceSelection();
            if (audience == (int)DispatchAudience.Partners)
            {
                selection.Audience = DispatchAudience.Partners;
                if (partnerId.HasValue && batchId.HasValue)
                    selection.PartnerBatchIds[partnerId.Value] = new List<int> { batchId.Value };
            }

            var vm = await builder.BuildAsync(User, new DispatchSource(PartnerDispatchKind.Homework, modelId, null, null), selection, includePartners);
            return PartialView("~/Areas/Admin/Views/Shared/_DispatchAudiencePicker.cshtml", vm);
        }

        [HttpPost("bind")]
        [IgnoreAntiforgeryToken]
        public IActionResult Bind(AudienceSelection selection) => Json(selection);
    }

    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class AdminPartnerDispatchIntegrationTests : IClassFixture<ApdWebApplicationFactory>, IAsyncLifetime
    {
        private const string OwnerUser = "apds1-owner";
        private const string SendOnlyUser = "apds1-send-only";
        private const string NoDispatchUser = "apds1-no-dispatch";

        private readonly ApdWebApplicationFactory _factory;
        private ApdSeed _seed = null!;

        public AdminPartnerDispatchIntegrationTests(ApdWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ApdTestData.CleanupAsync(db);
            _seed = await ApdTestData.SeedAsync(db);

            await AddProfileUserAsync(db, SendOnlyUser, ("PartnerDispatch", new[] { "Send" }));
            await AddProfileUserAsync(db, NoDispatchUser, ("AdminLessonCompletions", new[] { "ManageModelHomework" }));
        }

        public async Task DisposeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            await ApdTestData.CleanupAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private static async Task AddProfileUserAsync(ApplicationDbContext db, string userId, (string Controller, string[] Actions) permission)
        {
            db.Users.Add(ApdTestData.NewUser(userId));
            var profile = new AdminProfile { Name = $"{ApdTestData.Marker} {userId}" };
            db.AdminProfiles.Add(profile);
            await db.SaveChangesAsync();

            var row = new AdminProfileControllerPermission
            {
                AdminProfileId = profile.Id, ControllerName = permission.Controller, AccessLevel = AdminControllerAccessLevel.Read, HasCustomPermissions = true
            };
            foreach (var action in permission.Actions)
                row.CustomPermissions.Add(new AdminProfileCustomPermission { ActionName = action, IsAllowed = true });
            db.AdminProfileControllerPermissions.Add(row);
            db.AdminUserProfiles.Add(new AdminUserProfile { UserId = userId, AdminProfileId = profile.Id });
            await db.SaveChangesAsync();
        }

        private HttpClient Client(string userId, string role)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
            return client;
        }

        private HttpClient Owner() => Client(OwnerUser, "Owner");

        private static async Task<string> HtmlAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        }

        private string OptionsUrl(int? modelId = null, int kind = (int)PartnerDispatchKind.Homework)
            => $"/Admin/PartnerDispatch/AudienceOptions?kind={kind}&modelId={modelId ?? _seed.ModelLinkedToA}";

        // يستخرج وسم input الخاص بدفعة معيّنة
        private static string InputFor(string html, string name, int value)
        {
            var match = Regex.Match(html, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*value=\"{value}\"[^>]*>");
            Assert.True(match.Success, $"لا يوجد حقل {name}={value}");
            return match.Value;
        }

        // =====================================================
        // AudienceOptions
        // =====================================================
        [Fact]
        public async Task AudienceOptions_Owner_RendersPartners_Batches_Reasons_AndOverride()
        {
            var html = await HtmlAsync(await Owner().GetAsync(OptionsUrl()));

            Assert.Contains($"{ApdTestData.Marker} مدرسة A", html);
            Assert.Contains($"data-apd-partner=\"{_seed.PartnerA}\"", html);

            // دفعة A1 مُحددة افتراضيًا ومعطّلة حتى يُختار الشريك
            var a1 = InputFor(html, $"PartnerBatchIds[{_seed.PartnerA}]", _seed.BatchA1);
            Assert.Contains("checked", a1);
            Assert.Contains("disabled", a1);

            // الدفعة بلا طلاب نشطين لا تُحدد
            var empty = InputFor(html, $"PartnerBatchIds[{_seed.PartnerA}]", _seed.BatchAEmpty);
            Assert.DoesNotContain("checked", empty);
            Assert.Contains("data-apd-empty-batch=\"1\"", empty);

            // لا دفعات خارج المنهج أو مؤرشفة أو للمعهد
            Assert.DoesNotContain($"value=\"{_seed.BatchA3OtherCurriculum}\"", html);
            Assert.DoesNotContain($"value=\"{_seed.BatchA2Archived}\"", html);
            Assert.DoesNotContain($"value=\"{_seed.BatchI1}\"", html);

            Assert.Contains(DispatchRules.FeatureMissing(PartnerDispatchKind.Homework), html);
            Assert.Contains(DispatchRules.NoActiveContract, html);
            Assert.DoesNotContain($"{ApdTestData.Marker} مدرسة D", html);

            // المالك يملك Override: خيار التجاوز وحقل السبب (معطّل حتى التفعيل)
            Assert.Contains("name=\"OverrideContract\"", html);
            Assert.Matches("<textarea[^>]*name=\"OverrideReason\"[^>]*disabled", html);
        }

        [Fact]
        public async Task AudienceOptions_SendWithoutOverride_HidesOverride()
        {
            var html = await HtmlAsync(await Client(SendOnlyUser, "Admin").GetAsync(OptionsUrl()));

            Assert.Contains($"data-apd-partner=\"{_seed.PartnerA}\"", html);
            Assert.DoesNotContain("name=\"OverrideContract\"", html);
            Assert.DoesNotContain("name=\"OverrideReason\"", html);

            // الشريك القابل للتجاوز يبقى معطّلًا بلا صلاحية التجاوز
            Assert.Matches($"data-apd-partner=\"{_seed.PartnerB}\"[^>]*data-apd-overridable=\"0\"", html);
        }

        [Fact]
        public async Task AudienceOptions_WithoutSendPermission_IsRejected()
        {
            var response = await Client(NoDispatchUser, "Admin").GetAsync(OptionsUrl());
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AudienceOptions_UnknownKind_IsBadRequest()
        {
            var response = await Owner().GetAsync(OptionsUrl(kind: 99));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // =====================================================
        // الـ Picker بلا JavaScript
        // =====================================================
        [Fact]
        public async Task Picker_Default_IsPlatform_WithInstituteBatchesOnly()
        {
            var html = await HtmlAsync(await Owner().GetAsync($"/__apd-test/picker?modelId={_seed.ModelLinkedToA}"));

            Assert.Matches($"<input[^>]*name=\"Audience\"[^>]*value=\"{(int)DispatchAudience.Platform}\"[^>]*checked", html);
            Assert.Contains($"value=\"{(int)DispatchAudience.Partners}\"", html);

            var i1 = InputFor(html, "PlatformBatchIds", _seed.BatchI1);
            Assert.DoesNotContain("disabled", i1);
            Assert.DoesNotContain($"value=\"{_seed.BatchA1}\"", html);

            Assert.Contains("data-apd-loaded=\"0\"", html);
            Assert.Contains("/Admin/PartnerDispatch/AudienceOptions", html);
            Assert.Contains($"data-apd-model-id=\"{_seed.ModelLinkedToA}\"", html);
            Assert.Contains("<noscript>", html);
        }

        [Fact]
        public async Task Picker_WithoutSendPermission_ShowsPlatformOnly()
        {
            var html = await HtmlAsync(await Client(NoDispatchUser, "Admin").GetAsync($"/__apd-test/picker?modelId={_seed.ModelLinkedToA}&audience=2"));

            Assert.DoesNotContain($"value=\"{(int)DispatchAudience.Partners}\"", html);
            Assert.DoesNotContain($"data-apd-panel=\"{(int)DispatchAudience.Partners}\"", html);
            Assert.Matches($"<input[^>]*name=\"Audience\"[^>]*value=\"{(int)DispatchAudience.Platform}\"[^>]*checked", html);
        }

        [Fact]
        public async Task Picker_RedisplaysPartnerSelection_FromServer()
        {
            var url = $"/__apd-test/picker?modelId={_seed.ModelLinkedToA}&audience=2&partnerId={_seed.PartnerA}&batchId={_seed.BatchA1}&includePartners=true";
            var html = await HtmlAsync(await Owner().GetAsync(url));

            Assert.Contains("data-apd-loaded=\"1\"", html);
            Assert.Matches($"<input[^>]*name=\"Audience\"[^>]*value=\"{(int)DispatchAudience.Partners}\"[^>]*checked", html);
            Assert.Contains("disabled", InputFor(html, "PlatformBatchIds", _seed.BatchI1));

            var a1 = InputFor(html, $"PartnerBatchIds[{_seed.PartnerA}]", _seed.BatchA1);
            Assert.Contains("checked", a1);
            Assert.DoesNotContain("disabled", a1);
        }

        [Fact]
        public async Task FormFieldNames_BindToAudienceSelection()
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                new("Audience", "2"),
                new("PartnerBatchIds[11]", "101"),
                new("PartnerBatchIds[11]", "102"),
                new("PartnerBatchIds[22]", "201"),
                new("PlatformBatchIds", "5"),
                new("OverrideContract", "true"),
                new("OverrideReason", "سبب مكتوب كافٍ للتجاوز")
            };

            var response = await Owner().PostAsync("/__apd-test/bind", new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var selection = JsonSerializer.Deserialize<AudienceSelection>(await response.Content.ReadAsStringAsync())!;
            Assert.Equal(DispatchAudience.Partners, selection.Audience);
            Assert.Equal(new[] { 101, 102 }, selection.PartnerBatchIds[11]);
            Assert.Equal(new[] { 201 }, selection.PartnerBatchIds[22]);
            Assert.Equal(new[] { 5 }, selection.PlatformBatchIds);
            Assert.True(selection.OverrideContract);
            Assert.Equal("سبب مكتوب كافٍ للتجاوز", selection.OverrideReason);
        }

        [Fact]
        public async Task KillSwitch_HidesPartnersOption_AndClosesAudienceOptions_PlatformUnaffected()
        {
            async Task SetAsync(string value)
            {
                using var scope = _factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var updated = await db.SystemSettings
                    .Where(s => s.Key == DispatchAudiencePickerBuilder.EnabledSettingKey)
                    .ExecuteUpdateAsync(x => x.SetProperty(s => s.Value, value));
                if (updated == 0)
                {
                    db.SystemSettings.Add(new SystemSetting(DispatchAudiencePickerBuilder.EnabledSettingKey, value, "APD test"));
                    await db.SaveChangesAsync();
                }
            }

            await SetAsync("false");
            try
            {
                Assert.Equal(HttpStatusCode.NotFound, (await Owner().GetAsync(OptionsUrl())).StatusCode);

                var html = await HtmlAsync(await Owner().GetAsync($"/__apd-test/picker?modelId={_seed.ModelLinkedToA}&audience=2"));
                Assert.DoesNotContain($"data-apd-panel=\"{(int)DispatchAudience.Partners}\"", html);
                Assert.DoesNotContain("disabled", InputFor(html, "PlatformBatchIds", _seed.BatchI1));
            }
            finally
            {
                await SetAsync("true");
            }

            Assert.Equal(HttpStatusCode.OK, (await Owner().GetAsync(OptionsUrl())).StatusCode);
        }

        // =====================================================
        // علَم عقد معمل القياس في شاشات الاشتراك
        // =====================================================
        [Fact]
        public async Task SubscriptionCreate_ShowsMinistrySimFlag()
        {
            // C بلا عقد نشط (الشريك صاحب العقد النشط يُعاد توجيهه بقصد)
            var html = await HtmlAsync(await Owner().GetAsync($"/Admin/PartnerSubscriptions/Create?partnerId={_seed.PartnerC}"));
            Assert.Contains("name=\"CanUseMinistrySimExams\"", html);
            Assert.Contains("معمل القياس", html);
        }

        [Fact]
        public async Task SubscriptionDetails_ShowsMinistrySimFlag()
        {
            int subscriptionId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                subscriptionId = await db.PartnerSubscriptions.Where(s => s.PartnerId == _seed.PartnerB).Select(s => s.Id).SingleAsync();
            }

            var html = await HtmlAsync(await Owner().GetAsync($"/Admin/PartnerSubscriptions/Details/{subscriptionId}"));
            Assert.Contains("معمل القياس", html);
        }
    }
}
