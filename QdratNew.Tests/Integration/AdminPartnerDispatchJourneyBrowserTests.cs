using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Tests.AdminPartnerDispatch;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // APD-S6.2 — رحلة معمل القياس وسجل الإرسال بمتصفح حقيقي (المنفذ 5311):
    // الأدمن يسند الاختبار لمدرستين من الـ Picker ← لا يظهر في شاشة تكليفات الأدمن ← الطالب يراه في لوحته
    // ← بعد اكتمال محاولته تراه مدرسته فقط ← سجل الإرسال يعرض العملية ويفلتر ويصدّر.
    // الواجب والاختبار وتحديد المستوى ومؤشر الأداء لها رحلاتها في PartnerModelsJourneyBrowserTests.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "E2E")]
    public sealed class AdminPartnerDispatchJourneyBrowserTests : IAsyncLifetime
    {
        private const string OwnerUser = "apds1-e2e-owner";
        private const string PartnerAUser = "apds1-e2e-pa";
        private const string PartnerBUser = "apds1-e2e-pb";
        private const string StudentUser = "apds1-sa2"; // طالب A2 من PrepareMinistrySimAsync

        private RealServerFixture? _server;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private ApdSeed _seed = null!;
        private ApdMinistrySimSeed _sim = null!;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            await using (var db = RtkRealDb.CreateDb())
            {
                await ApdTestData.CleanupAsync(db);
                _seed = await ApdTestData.SeedAsync(db);
                _sim = await ApdTestData.PrepareMinistrySimAsync(db, _seed);

                var hasher = new PasswordHasher<ApplicationUser>();
                ApplicationUser WithPassword(ApplicationUser user)
                {
                    user.PasswordHash = hasher.HashPassword(user, RtkRealDb.Password);
                    user.EmailConfirmed = true;
                    return user;
                }

                db.Users.AddRange(
                    WithPassword(ApdTestData.NewUser(OwnerUser)),
                    WithPassword(ApdTestData.NewUser(PartnerAUser)),
                    WithPassword(ApdTestData.NewUser(PartnerBUser)));

                var student = await db.Users.SingleAsync(u => u.Id == StudentUser);
                WithPassword(student);

                async Task<string> RoleAsync(string name) => await db.Roles.Where(r => r.Name == name).Select(r => r.Id).FirstAsync();
                db.UserRoles.AddRange(
                    new IdentityUserRole<string> { UserId = OwnerUser, RoleId = await RoleAsync("Owner") },
                    new IdentityUserRole<string> { UserId = PartnerAUser, RoleId = await RoleAsync("Partner") },
                    new IdentityUserRole<string> { UserId = PartnerBUser, RoleId = await RoleAsync("Partner") },
                    new IdentityUserRole<string> { UserId = StudentUser, RoleId = await RoleAsync("Student") });
                db.UserPartners.AddRange(
                    new UserPartner { UserId = PartnerAUser, PartnerId = _seed.PartnerA },
                    new UserPartner { UserId = PartnerBUser, PartnerId = _seed.PartnerB });
                await db.SaveChangesAsync();
            }

            _server = await RealServerFixture.StartAsync(5311);
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }

        public async Task DisposeAsync()
        {
            if (_browser != null) await _browser.DisposeAsync();
            _playwright?.Dispose();
            if (_server != null) await _server.DisposeAsync();

            if (RtkRealDb.IsConfigured)
            {
                await using var db = RtkRealDb.CreateDb();
                await db.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUserRoles WHERE UserId LIKE 'apds1-%'");
                await ApdTestData.CleanupAsync(db);
            }
        }

        private async Task<IPage> LoginAsync(string userName, bool mobile = false)
        {
            var options = new BrowserNewContextOptions { BaseURL = _server!.BaseUrl, IgnoreHTTPSErrors = true, AcceptDownloads = true };
            if (mobile)
            {
                options.ViewportSize = new ViewportSize { Width = 375, Height = 812 };
                options.IsMobile = true;
                options.HasTouch = true;
            }

            var page = await (await _browser!.NewContextAsync(options)).NewPageAsync();
            await page.GotoAsync("/LMS/login");
            await page.FillAsync("#Input_Email", userName);
            await page.FillAsync("#passwordField", RtkRealDb.Password);
            await page.ClickAsync("button[type=submit]");
            await page.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });
            return page;
        }

        private async Task<IPage> LoginPartnerAsync(string userName, int partnerId)
        {
            var page = await LoginAsync(userName, mobile: true);
            await page.GotoAsync("/Partner/PartnerContext/Select");
            await page.SelectOptionAsync("select[name=partnerId]", partnerId.ToString());
            await page.ClickAsync("form button[type=submit]");
            await page.WaitForLoadStateAsync();
            return page;
        }

        [SqlServerFact]
        public async Task Admin_AssignsLabToTwoSchools_EachSeesOnlyItsStudents_AdminDoesNot_LogShowsAndExports()
        {
            var admin = await LoginAsync(OwnerUser);
            var pageErrors = new List<string>();
            admin.PageError += (_, message) => pageErrors.Add(message);

            // ---------- الإسناد من الـ Picker ----------
            await admin.GotoAsync($"/Admin/MinistrySimExam/AssignToBatch/{_sim.ExamId}");
            Assert.Equal(1, await admin.Locator($"#apd-platform-{_seed.BatchI1}").CountAsync());
            Assert.Equal(0, await admin.Locator($"#apd-platform-{_seed.BatchA1}").CountAsync());

            await admin.CheckAsync($"input[name=Audience][value='{(int)DispatchAudience.Partners}']");
            await admin.Locator($"#apd-partner-{_seed.PartnerA}").WaitForAsync();
            await admin.CheckAsync($"#apd-partner-{_seed.PartnerA}");
            await admin.CheckAsync($"#apd-partner-{_seed.PartnerB}");
            Assert.True(await admin.IsCheckedAsync($"#apd-batch-{_seed.PartnerA}-{_seed.BatchA1}"));
            Assert.True(await admin.IsCheckedAsync($"#apd-batch-{_seed.PartnerB}-{_seed.BatchB1}"));

            await admin.ClickAsync("#mseAssignBatchSubmit");
            await admin.WaitForURLAsync(url => url.Contains("/PartnerDispatch/Result"), new PageWaitForURLOptions { Timeout = 20000 });
            Assert.Contains("نجح", await admin.InnerTextAsync($"[data-apd-result-partner='{_seed.PartnerA}']"));
            Assert.Contains("نجح", await admin.InnerTextAsync($"[data-apd-result-partner='{_seed.PartnerB}']"));

            // ---------- الأدمن لا يرى تكليفات المدارس ----------
            await admin.GotoAsync("/Admin/MinistrySimExamAssignments");
            var adminBody = await admin.InnerTextAsync("body");
            Assert.DoesNotContain($"{ApdTestData.Marker} دفعة A1", adminBody);
            Assert.DoesNotContain($"{ApdTestData.Marker} دفعة B1", adminBody);

            // ---------- الطالب يرى الاختبار في لوحته (جوال) ----------
            var student = await LoginAsync(StudentUser, mobile: true);
            await student.GotoAsync("/Students/MinistrySimExam/Dashboard");
            Assert.Contains($"{ApdTestData.Marker} معمل قياس", await student.InnerTextAsync("body"));

            // اكتمال الحل (مسار الحل نفسه مغطى في اختبارات الطالب؛ هنا تهمنا الملكية بعده)
            await using (var db = RtkRealDb.CreateDb())
            {
                await ApdTestData.CompleteSimAttemptAsync(db, _sim, _sim.StudentA2, score: 50);
            }

            // ---------- المدرسة A ترى طالبها ودرجته ----------
            var partnerA = await LoginPartnerAsync(PartnerAUser, _seed.PartnerA);
            await partnerA.GotoAsync("/Partner/MinistrySimExams");
            Assert.Equal(1, await partnerA.Locator($"tr[data-mse-exam='{_sim.ExamId}']").CountAsync());

            await partnerA.GotoAsync($"/Partner/MinistrySimExams/BatchStudents/{_sim.ExamId}?batchId={_seed.BatchA1}");
            Assert.Contains("50%", await partnerA.InnerTextAsync($"tr[data-mse-student='{_sim.StudentA2}']"));

            // ---------- المدرسة B لا ترى طالب A ولا دفعته ----------
            var partnerB = await LoginPartnerAsync(PartnerBUser, _seed.PartnerB);
            await partnerB.GotoAsync($"/Partner/MinistrySimExams/BatchStudents/{_sim.ExamId}");
            Assert.Equal(0, await partnerB.Locator($"tr[data-mse-student='{_sim.StudentA2}']").CountAsync());
            var leak = await partnerB.GotoAsync($"/Partner/MinistrySimExams/StudentResult/{_sim.ExamId}?studentId={_sim.StudentA2}");
            Assert.NotEqual(200, leak!.Status);

            // ---------- سجل الإرسال ----------
            await admin.GotoAsync("/Admin/PartnerDispatchLog");
            var group = admin.Locator("details.pdl-group", new PageLocatorOptions { HasTextString = "معمل القياس" });
            Assert.Equal(1, await group.CountAsync());
            Assert.Contains("2 مدرسة · 2 دفعة", await group.Locator("summary").InnerTextAsync());

            await group.Locator("summary").ClickAsync();
            Assert.True(await group.Locator("table").IsVisibleAsync());
            Assert.Contains($"{ApdTestData.Marker} مدرسة B", await group.Locator("table").InnerTextAsync());

            await admin.SelectOptionAsync("#PartnerId", _seed.PartnerA.ToString());
            await admin.ClickAsync("#pdlFilters button[type=submit]");
            await admin.WaitForURLAsync(url => url.Contains($"PartnerId={_seed.PartnerA}"), new PageWaitForURLOptions { Timeout = 20000 });
            Assert.Contains("1 مدرسة · 1 دفعة", await admin.InnerTextAsync("details.pdl-group summary"));

            var download = await admin.RunAndWaitForDownloadAsync(() => admin.ClickAsync("#pdlExport"));
            Assert.EndsWith(".xlsx", download.SuggestedFilename);

            Assert.Empty(pageErrors);
        }
    }
}
