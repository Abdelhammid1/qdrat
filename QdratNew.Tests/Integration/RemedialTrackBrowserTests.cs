using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using QdratNew.Enums;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // RTK-S7.2 — E2E بمتصفح حقيقي (Playwright) على خادم QdratNew الحقيقي وقاعدة الاختبار المعزولة، على شاشة 375px (جوال):
    // بوابة الرقم المرجعي ← صفحة المحور ← الحل والحفظ التلقائي ← التسليم ← النتيجة، مع التحقق من RTL وعدم وجود تمرير أفقي.
    // إتمام الفيديو الفعلي لا يُختبر عبر مشغّل يوتيوب (يعتمد شبكة خارجية): تُهيَّأ حالة "بانتظار اختبار 101" مسبقًا في القاعدة.
    // المنفذ 5297 (QRT=5298، حماية الترجمة=5299) كي لا يتعارض مع أي منها. يتخطّى ذاتيًا إن غاب QDRAT_TEST_SQL.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "E2E")]
    public sealed class RemedialTrackBrowserTests : IAsyncLifetime
    {
        private RealServerFixture? _server;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private RtkSeed _seed = null!;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            _seed = await RtkRealDb.SeedAsync(students: 1, inPerson: true, withLogin: true);
            _server = await RealServerFixture.StartAsync(5297);
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }

        public async Task DisposeAsync()
        {
            if (_browser != null) await _browser.DisposeAsync();
            _playwright?.Dispose();
            if (_server != null) await _server.DisposeAsync();
            if (RtkRealDb.IsConfigured) await RtkRealDb.CleanupAsync();
        }

        private static async Task<string> PageErrorAsync(IPage page)
        {
            var text = await page.InnerTextAsync("body");
            return text.Length > 1200 ? text[..1200] : text;
        }

        // لا تمرير أفقي: عرض المستند ≤ عرض النافذة (هامش 1px للتقريب)
        private static async Task AssertNoHorizontalScrollAsync(IPage page, string where)
        {
            var overflow = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - window.innerWidth");
            Assert.True(overflow <= 1, $"تمرير أفقي في {where}: +{overflow}px");
        }

        private async Task GoOkAsync(IPage page, string url)
        {
            var r = await page.GotoAsync(url);
            Assert.True(r?.Ok, $"فشل تحميل {url}: {r?.Status}{Environment.NewLine}{await PageErrorAsync(page)}{Environment.NewLine}--- server ---{Environment.NewLine}{_server!.RecentOutput}");
        }

        [SqlServerFact]
        public async Task StudentJourney_OnMobile375_Gate_Axis_Solve_Submit_Result()
        {
            var s = _seed.Students[0];

            var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                BaseURL = _server!.BaseUrl,
                ViewportSize = new ViewportSize { Width = 375, Height = 812 },
                IsMobile = true,
                HasTouch = true
            });
            var page = await context.NewPageAsync();

            // ---------- دخول الطالب ----------
            var login = await page.GotoAsync("/LMS/login");
            Assert.True(login?.Ok, $"فشل تحميل صفحة الدخول: {login?.Status}");
            await page.FillAsync("#Input_Email", s.UserName);
            await page.FillAsync("#passwordField", RtkRealDb.Password);
            await page.ClickAsync("button[type=submit]");
            await page.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });

            // ---------- قائمة الخطة ----------
            await GoOkAsync(page, "/Students/RemedialTrack");
            Assert.Equal("rtl", await page.EvaluateAsync<string>("() => document.documentElement.dir"));
            await Assertions.Expect(page.Locator("body")).ToContainTextAsync(RtkRealDb.Marker);
            await AssertNoHorizontalScrollAsync(page, "قائمة الخطة");

            // ---------- بوابة الرقم المرجعي ----------
            await GoOkAsync(page, $"/Students/RemedialTrack/Open?enrollmentId={s.EnrollmentId}");
            var codeInput = page.Locator("input[name=Code]");
            await Assertions.Expect(codeInput).ToBeVisibleAsync();
            await AssertNoHorizontalScrollAsync(page, "بوابة الرقم");

            var wrong = _seed.AccessCode == "111111" ? "222222" : "111111";
            await codeInput.FillAsync(wrong);
            await page.ClickAsync("#rtkCodeSubmit");
            await Assertions.Expect(page.Locator(".rtk-alert-error")).ToBeVisibleAsync(new() { Timeout = 15000 });
            Assert.DoesNotContain(_seed.AccessCode!, await page.ContentAsync());               // لا يتسرب الرقم الصحيح

            await page.Locator("input[name=Code]").FillAsync(_seed.AccessCode!);
            await page.ClickAsync("#rtkCodeSubmit");
            await page.WaitForURLAsync(url => url.Contains("/RemedialTrack/Open"), new PageWaitForURLOptions { Timeout = 15000 });
            Assert.True(await page.Locator("input[name=Code]").CountAsync() == 0, "ما زالت البوابة ظاهرة بعد الرقم الصحيح");
            await AssertNoHorizontalScrollAsync(page, "صفحة الخطة");

            // ---------- صفحة المحور (الفيديوهات التسلسلية) ----------
            await GoOkAsync(page, $"/Students/RemedialTrack/Axis?enrollmentId={s.EnrollmentId}&axisProgressId={s.AxisProgressIds[0]}");
            await Assertions.Expect(page.Locator("#rtkVideoList > *")).ToHaveCountAsync(2);
            await AssertNoHorizontalScrollAsync(page, "صفحة المحور");

            // إتمام الفيديو يُهيَّأ في القاعدة (لا مشغّل يوتيوب): ثم يظهر زر بدء اختبار 101
            await using (var db = RtkRealDb.CreateDb())
            {
                await db.RemedialTrackVideoProgresses.Where(v => v.AxisProgressId == s.AxisProgressIds[0])
                    .ExecuteUpdateAsync(x => x.SetProperty(v => v.IsCompleted, true).SetProperty(v => v.EndedSeen, true)
                        .SetProperty(v => v.WatchedSeconds, 100d).SetProperty(v => v.CompletedAtUtc, DateTime.UtcNow));
                await db.RemedialTrackAxisProgresses.Where(a => a.Id == s.AxisProgressIds[0])
                    .ExecuteUpdateAsync(x => x.SetProperty(a => a.Status, RemedialTrackAxisStatus.AwaitingExam101));
            }
            await GoOkAsync(page, $"/Students/RemedialTrack/Axis?enrollmentId={s.EnrollmentId}&axisProgressId={s.AxisProgressIds[0]}");
            var startBtn = page.Locator("form[action*='StartExam'] button[type=submit]");
            await Assertions.Expect(startBtn).ToBeVisibleAsync(new() { Timeout = 15000 });
            Assert.True((await startBtn.BoundingBoxAsync())!.Height >= 44, "زر بدء الاختبار أقل من 44px");

            // ---------- الحل: حفظ تلقائي لكل إجابة ----------
            await startBtn.ClickAsync();
            await page.WaitForURLAsync(url => url.Contains("/RemedialTrack/Solve"), new PageWaitForURLOptions { Timeout = 20000 });
            var sections = page.Locator("section.rtk-q");
            await Assertions.Expect(sections).ToHaveCountAsync(4, new() { Timeout = 15000 });
            await AssertNoHorizontalScrollAsync(page, "واجهة الحل");

            // لا تسريب للإجابة/الشرح/الفيديو في الصفحة قبل التسليم
            var solveHtml = await page.ContentAsync();
            Assert.DoesNotContain(RtkRealDb.SecretExplanation, solveHtml);
            Assert.DoesNotContain(RtkRealDb.SecretVideoUrl, solveHtml);

            for (var i = 0; i < 4; i++)
            {
                var section = sections.Nth(i);
                await section.Locator("label.qx-option", new() { HasText = "الجواب الصحيح" }).ClickAsync();
                await Assertions.Expect(section.Locator("[data-role=saved]")).ToBeVisibleAsync(new() { Timeout = 15000 });
                // سؤال واحد في كل مرة: انتقل للتالي، وعند آخر سؤال افتح لوحة المراجعة
                if (i < 3) await page.ClickAsync("[data-rtk-nav=next]");
                else await page.ClickAsync("[data-rtk-nav=showReview]");
            }
            await Assertions.Expect(page.Locator("#rtkAnsweredCount")).ToHaveTextAsync("4");
            await Assertions.Expect(page.Locator("#rtkReviewPanel")).ToBeVisibleAsync();

            // (النص يمرّ على smart-text فتتحوّل أرقامه لهندية في العرض؛ لذا يُنتقى بالجزء العربي فقط) الحفظ وصل القاعدة فعلًا
            await using (var db = RtkRealDb.CreateDb())
                Assert.Equal(4, await db.RemedialTrackExamAttemptQuestions.CountAsync(
                    x => x.Attempt!.AxisProgressId == s.AxisProgressIds[0] && x.SelectedAnswer == RtkRealDb.CorrectAnswer));

            // ---------- التسليم والنتيجة ----------
            await page.ClickAsync("#rtkSubmitBtn");
            // تأكيد الإنهاء: SweetAlert يُحمَّل من CDN؛ إن تعذّر تحميله يُسلَّم مباشرة (المسار الاحتياطي في remedial-track-exam.js)
            try { await page.ClickAsync(".swal2-confirm", new PageClickOptions { Timeout = 6000 }); }
            catch (TimeoutException) { /* لا نافذة تأكيد */ }
            await page.WaitForURLAsync(url => url.Contains("/RemedialTrack/Result"), new PageWaitForURLOptions { Timeout = 20000 });
            await Assertions.Expect(page.Locator(".rs-message h2")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await AssertNoHorizontalScrollAsync(page, "صفحة النتيجة");

            var resultHtml = await page.ContentAsync();
            Assert.DoesNotContain(_seed.AccessCode!, resultHtml);

            await using var check = RtkRealDb.CreateDb();
            var aps = await check.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(a => a.EnrollmentId == s.EnrollmentId).OrderBy(a => a.Order).ToListAsync();
            Assert.Equal(RemedialTrackAxisStatus.Passed, aps[0].Status);
            Assert.Equal(RemedialTrackAxisStatus.Videos, aps[1].Status);

            await context.CloseAsync();
        }
    }
}
