using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using QdratNew.Entities;
using QdratNew.Enums;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // PPM-S1.3 — قسم «الإتاحة للشركاء» بمتصفح حقيقي على 375px وقاعدة الاختبار المعزولة:
    // الإظهار/الإخفاء، البحث، تحديد الكل، العدّاد، ثم الحفظ الفعلي. المنفذ 5305.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "E2E")]
    public class ProfessionalModelPartnersBrowserTests : IAsyncLifetime
    {
        private const string UserName = "ppme2e-owner";
        private const string Marker = "PPME2E";

        private RealServerFixture? _server;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private int _partnerA;
        private int _partnerB;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            await CleanupAsync();

            await using (var db = RtkRealDb.CreateDb())
            {
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = UserName,
                    NormalizedUserName = UserName.ToUpperInvariant(),
                    Email = $"{UserName}@test.local",
                    NormalizedEmail = $"{UserName}@test.local".ToUpperInvariant(),
                    FullName = UserName,
                    NationalID = UserName,
                    IsActive = true,
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                };
                user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, RtkRealDb.Password);
                db.Users.Add(user);

                var roleId = await db.Roles.Where(r => r.Name == "Owner").Select(r => r.Id).FirstAsync();
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleId });

                var a = new Partner { Name = $"{Marker} مدرسة النور", Code = $"{Marker}A", PartnershipStart = DateTime.Today.AddDays(-1), PartnershipEnd = DateTime.Today.AddDays(9) };
                var b = new Partner { Name = $"{Marker} مدرسة الفجر", Code = $"{Marker}B", PartnershipStart = DateTime.Today.AddDays(-1), PartnershipEnd = DateTime.Today.AddDays(9) };
                db.Partners.AddRange(a, b);
                await db.SaveChangesAsync();
                _partnerA = a.Id;
                _partnerB = b.Id;
            }

            _server = await RealServerFixture.StartAsync(5305);
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }

        public async Task DisposeAsync()
        {
            if (_browser != null) await _browser.DisposeAsync();
            _playwright?.Dispose();
            if (_server != null) await _server.DisposeAsync();
            if (RtkRealDb.IsConfigured) await CleanupAsync();
        }

        private static async Task CleanupAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            foreach (var sql in new[]
            {
                "DELETE FROM ProfessionalModelPartners WHERE ProfessionalModelId IN (SELECT Id FROM ProfessionalModels WHERE Title LIKE 'PPME2E%')",
                "DELETE FROM ProfessionalModels WHERE Title LIKE 'PPME2E%'",
                "DELETE FROM Partners WHERE Code LIKE 'PPME2E%'",
                "DELETE FROM AspNetUserRoles WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE UserName = 'ppme2e-owner')",
                "DELETE FROM AspNetUsers WHERE UserName = 'ppme2e-owner'"
            })
                await db.Database.ExecuteSqlRawAsync(sql);
        }

        private async Task<IPage> LoginOnMobileAsync(List<string> pageErrors)
        {
            var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                BaseURL = _server!.BaseUrl,
                ViewportSize = new ViewportSize { Width = 375, Height = 800 }
            });
            var page = await context.NewPageAsync();
            await page.GotoAsync("/LMS/login");
            await page.FillAsync("#Input_Email", UserName);
            await page.FillAsync("#passwordField", RtkRealDb.Password);
            await page.ClickAsync("button[type=submit]");
            await page.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });

            page.PageError += (_, e) => pageErrors.Add(e);
            return page;
        }

        [SqlServerFact]
        public async Task Create_Section_TogglesSearchesAndSaves()
        {
            var errors = new List<string>();
            var page = await LoginOnMobileAsync(errors);

            var response = await page.GotoAsync("/Admin/ProfessionalModels/Create");
            Assert.True(response?.Ok);

            var body = page.Locator("[data-ppm-audience-body]");
            var panel = page.Locator("[data-ppm-partners-panel]");

            // افتراضيًا: داخلي، والأقسام مخفية
            Assert.False(await body.IsVisibleAsync());

            await page.Locator("#IsForPartners").CheckAsync();
            Assert.True(await body.IsVisibleAsync());
            Assert.False(await panel.IsVisibleAsync());

            await page.Locator("#PartnerAudienceSelected").CheckAsync();
            Assert.True(await panel.IsVisibleAsync());
            Assert.True(await page.Locator("[data-ppm-tools]").IsVisibleAsync());

            // البحث يخفي غير المطابق، و«تحديد الكل» يخص الظاهر فقط
            await page.Locator("[data-ppm-search]").FillAsync($"{Marker}A");
            var partnerBox = page.Locator($"input[name=SelectedPartnerIds][value='{_partnerA}']");
            var otherBox = page.Locator($"input[name=SelectedPartnerIds][value='{_partnerB}']");
            Assert.True(await partnerBox.IsVisibleAsync());
            Assert.False(await otherBox.IsVisibleAsync());

            await page.Locator("[data-ppm-select-all]").ClickAsync();
            Assert.Equal("1", await page.Locator("[data-ppm-count]").InnerTextAsync());
            Assert.True(await partnerBox.IsCheckedAsync());
            Assert.False(await otherBox.IsCheckedAsync());

            await page.Locator("[data-ppm-search]").FillAsync("لا يوجد بهذا الاسم");
            Assert.True(await page.Locator("[data-ppm-no-results]").IsVisibleAsync());
            await page.Locator("[data-ppm-search]").FillAsync("");

            // لا تمرير أفقي داخل القسم على 375px
            var overflow = await page.Locator("[data-ppm-audience]").EvaluateAsync<bool>("e => e.scrollWidth > e.clientWidth + 1");
            Assert.False(overflow);

            var title = $"{Marker} نموذج";
            await page.FillAsync("#Title", title);
            await page.FillAsync("#Description", "-");
            await page.Locator("form button[type=submit]").Last.ClickAsync();
            await page.WaitForURLAsync(url => url.Contains("/ProfessionalModels/Details"), new PageWaitForURLOptions { Timeout = 20000 });

            await using var db = RtkRealDb.CreateDb();
            var model = await db.ProfessionalModels.AsNoTracking().SingleAsync(m => m.Title == title);
            Assert.True(model.IsForPartners);
            Assert.False(model.IsGlobal);
            Assert.Equal(_partnerA, (await db.ProfessionalModelPartners.SingleAsync(p => p.ProfessionalModelId == model.Id)).PartnerId);

            // إعادة الفتح في Edit تعرض الاختيار المحفوظ
            await page.GotoAsync($"/Admin/ProfessionalModels/Edit/{model.Id}");
            Assert.True(await page.Locator("#IsForPartners").IsCheckedAsync());
            Assert.True(await page.Locator("#PartnerAudienceSelected").IsCheckedAsync());
            Assert.True(await page.Locator($"input[name=SelectedPartnerIds][value='{_partnerA}']").IsCheckedAsync());
            Assert.Equal("1", await page.Locator("[data-ppm-count]").InnerTextAsync());

            Assert.Empty(errors);
        }
    }
}
