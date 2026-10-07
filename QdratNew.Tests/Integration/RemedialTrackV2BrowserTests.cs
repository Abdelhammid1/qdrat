using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // RTK-S12.3 — E2E بمتصفح حقيقي (Playwright) لحزمة v2 على خادم QdratNew الحقيقي وقاعدة الاختبار المعزولة:
    //  • ملء الشاشة على 375×812 مع حجب requestFullscreen ← يعمل الوضع البديل (S8.3) ويخرج بالزر وEscape.
    //  • وضع المراجعة (S10): شريط المراجعة + data-review + لا طلبات VideoPing + لا كتابة تقدّم؛ وبعد الانتهاء/الإغلاق لا فيديو.
    //  • صفحة ولي الأمر (S11): RTL، بلا تمرير أفقي على 375px، نص المسؤولية، وطباعة نظيفة.
    // لا يُختبر تشغيل فيديو حقيقي (YouTube شبكة خارجية). المنفذ 5300 كي لا يتعارض مع بقية اختبارات E2E.
    // ملاحظة: الخادم الفرعي يُشغَّل من bin/Debug (انظر RealServerFixture) — ابنِ المشروع الرئيسي Debug قبل التشغيل.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "E2E")]
    public sealed class RemedialTrackV2BrowserTests : IAsyncLifetime
    {
        private const string XssName = "<script>alert(1)</script>";

        private sealed class RealTz : ITimeZoneService
        {
            public DateTime GetNowUtc() => DateTime.UtcNow;
            public DateTime GetNowSaudi() => DateTime.UtcNow.AddHours(3);
            public DateTime ConvertToSaudi(DateTime utcTime) => utcTime.AddHours(3);
            public DateTime ConvertToUtc(DateTime saudiTime) => saudiTime.AddHours(-3);
        }

        private RealServerFixture? _server;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private RtkSeed _seed = null!;
        private string _parentUserName = "";
        private int _reportId;

        // 0: ملء الشاشة (محور 1 = فيديوهات) · 1: مراجعة (محور 1 = اجتاز) · 2: ابن ولي الأمر (راسب في 102)
        private const int Fs = 0, Review = 1, Child = 2;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            _seed = await RtkRealDb.SeedAsync(students: 3, withLogin: true, firstAxisStatus: RemedialTrackAxisStatus.Videos);

            await using (var db = RtkRealDb.CreateDb())
            {
                var r = _seed.Students[Review];
                await db.RemedialTrackAxisProgresses.Where(a => a.Id == r.AxisProgressIds[0])
                    .ExecuteUpdateAsync(x => x.SetProperty(a => a.Status, RemedialTrackAxisStatus.Passed));
                await db.RemedialTrackEnrollments.Where(e => e.Id == r.EnrollmentId)
                    .ExecuteUpdateAsync(x => x.SetProperty(e => e.VideoReviewEnabled, true));

                var childId = _seed.Students[Child].StudentId;
                await db.Students.Where(s => s.StudentID == childId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, XssName));
                await db.RemedialTrackAxisProgresses.Where(a => a.Id == _seed.Students[Child].AxisProgressIds[0])
                    .ExecuteUpdateAsync(x => x.SetProperty(a => a.Status, RemedialTrackAxisStatus.AwaitingExam102));
            }

            var (_, parentUserId) = await RtkRealDb.LinkParentAsync(_seed.Students[Child].StudentId, 21, withLogin: true);
            _parentUserName = "rtks7-p021";
            _ = parentUserId;

            var progress = new RemedialTrackProgressService(new RtkRealDb.Factory(), TimeProvider.System, new RealTz(),
                NullLogger<RemedialTrackProgressService>.Instance);
            await progress.OnExamSubmittedAsync(await RtkRealDb.AddFailedAttempt102Async(_seed, _seed.Students[Child]));

            await using (var db = RtkRealDb.CreateDb())
                _reportId = await db.RemedialTrackParentReports.AsNoTracking()
                    .Where(r => r.EnrollmentId == _seed.Students[Child].EnrollmentId).Select(r => r.Id).SingleAsync();

            _server = await RealServerFixture.StartAsync(5300);
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

        // ───────────── أدوات ─────────────

        private async Task<IPage> LoginAsync(string userName, int width = 375, int height = 812, bool blockFullscreen = false)
        {
            var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                BaseURL = _server!.BaseUrl,
                ViewportSize = new ViewportSize { Width = width, Height = height },
                IsMobile = true,
                HasTouch = true
            });
            if (blockFullscreen)
                await context.AddInitScriptAsync(
                    "Element.prototype.requestFullscreen = undefined; Element.prototype.webkitRequestFullscreen = undefined; Element.prototype.msRequestFullscreen = undefined;");

            var page = await context.NewPageAsync();
            var login = await page.GotoAsync("/LMS/login");
            Assert.True(login?.Ok, $"فشل تحميل صفحة الدخول: {login?.Status}");
            await page.FillAsync("#Input_Email", userName);
            await page.FillAsync("#passwordField", RtkRealDb.Password);
            await page.ClickAsync("button[type=submit]");
            await page.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });
            return page;
        }

        private async Task GoOkAsync(IPage page, string url)
        {
            var r = await page.GotoAsync(url);
            var body = await page.InnerTextAsync("body");
            Assert.True(r?.Ok, $"فشل تحميل {url}: {r?.Status}{Environment.NewLine}{(body.Length > 800 ? body[..800] : body)}{Environment.NewLine}--- server ---{Environment.NewLine}{_server!.RecentOutput}");
        }

        private static async Task AssertNoHorizontalScrollAsync(IPage page, string where)
        {
            var overflow = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - window.innerWidth");
            Assert.True(overflow <= 1, $"تمرير أفقي في {where}: +{overflow}px");
        }

        private string AxisUrl(int student)
        {
            var s = _seed.Students[student];
            return $"/Students/RemedialTrack/Axis?enrollmentId={s.EnrollmentId}&axisProgressId={s.AxisProgressIds[0]}";
        }

        // ───────────── S8.3: ملء الشاشة (الوضع البديل) ─────────────

        [SqlServerFact]
        public async Task Fullscreen_WhenApiBlocked_FallsBackToCssModeCoveringViewport_AndExitsByButtonAndEscape()
        {
            var page = await LoginAsync(_seed.Students[Fs].UserName, blockFullscreen: true);
            await GoOkAsync(page, AxisUrl(Fs));

            // المشغّل مخفي حتى يجهز فيديو يوتيوب (شبكة خارجية)؛ نُظهره لاختبار سلوك الزر نفسه
            await page.EvaluateAsync("() => { document.getElementById('rtkPlayerWrap').hidden = false; }");
            var btn = page.Locator("#rtkBtnFull");
            await Assertions.Expect(btn).ToBeVisibleAsync(new() { Timeout = 10000 });
            Assert.True((await btn.BoundingBoxAsync())!.Height >= 40, "هدف اللمس لزر ملء الشاشة صغير");

            await btn.ClickAsync();
            var wrap = page.Locator("#rtkPlayerWrap");
            await Assertions.Expect(wrap).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("rtk-pseudo-fs"));
            Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('rtk-no-scroll')"));

            var box = (await wrap.BoundingBoxAsync())!;
            Assert.True(Math.Abs(box.Width - 375) <= 1 && Math.Abs(box.Height - 812) <= 1, $"لا يغطي الشاشة: {box.Width}×{box.Height}");

            // العلامة المائية داخل الحاوية فتبقى ظاهرة في هذا الوضع، والزر ما زال قابلًا للنقر
            Assert.True(await page.Locator("#rtkPlayerWrap .rtk-watermark").CountAsync() >= 1, "العلامة المائية خارج الحاوية");
            await Assertions.Expect(btn).ToBeVisibleAsync();

            // الخروج بالزر نفسه
            await btn.ClickAsync();
            Assert.DoesNotContain("rtk-pseudo-fs", (await wrap.GetAttributeAsync("class")) ?? "");
            Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.classList.contains('rtk-no-scroll')"));

            // الخروج بـ Escape
            await btn.ClickAsync();
            await Assertions.Expect(wrap).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("rtk-pseudo-fs"));
            await page.Keyboard.PressAsync("Escape");
            Assert.DoesNotContain("rtk-pseudo-fs", (await wrap.GetAttributeAsync("class")) ?? "");
        }

        // ───────────── S10: وضع المراجعة ─────────────

        [SqlServerFact]
        public async Task ReviewMode_ShowsBanner_SendsNoPings_WritesNoProgress_AndClosesWhenDisabled()
        {
            var s = _seed.Students[Review];
            var page = await LoginAsync(s.UserName);

            var pings = 0;
            page.Request += (_, r) => { if (r.Url.Contains("VideoPing", StringComparison.OrdinalIgnoreCase)) pings++; };

            await GoOkAsync(page, AxisUrl(Review));
            await Assertions.Expect(page.Locator("#rtkReviewBanner")).ToBeVisibleAsync();
            Assert.Equal("1", await page.GetAttributeAsync("#rtkRoot", "data-review"));
            Assert.Equal(0, await page.Locator("#rtkManualDone").CountAsync());       // لا زر «أنهيت المشاهدة» في المراجعة
            await AssertNoHorizontalScrollAsync(page, "المراجعة");

            // تفاعل فعلي مع قائمة الفيديوهات ثم انتظار؛ لا نبضات (حارس JS) ولا أي كتابة تقدّم
            var first = page.Locator("#rtkVideoList > *").First;
            await first.ClickAsync();
            await page.WaitForTimeoutAsync(5000);
            Assert.Equal(0, pings);

            await using (var db = RtkRealDb.CreateDb())
            {
                Assert.Equal(RemedialTrackAxisStatus.Passed,
                    (await db.RemedialTrackAxisProgresses.AsNoTracking().SingleAsync(a => a.Id == s.AxisProgressIds[0])).Status);
                var vps = await db.RemedialTrackVideoProgresses.AsNoTracking().Where(v => v.AxisProgressId == s.AxisProgressIds[0]).ToListAsync();
                Assert.All(vps, v => { Assert.Null(v.LastPingAtUtc); Assert.Equal(0d, v.WatchedSeconds); Assert.False(v.IsCompleted); });

                // أغلقه الأدمن ← الإغلاق فوري من الخادم: لا شريط مراجعة ولا مشغّل ولا روابط
                await db.RemedialTrackEnrollments.Where(e => e.Id == s.EnrollmentId)
                    .ExecuteUpdateAsync(x => x.SetProperty(e => e.VideoReviewEnabled, false));
            }
            await GoOkAsync(page, AxisUrl(Review));
            Assert.Equal(0, await page.Locator("#rtkReviewBanner").CountAsync());
            Assert.Equal(0, await page.Locator("#rtkPlayerWrap").CountAsync());
            Assert.DoesNotContain("youtu", await page.ContentAsync(), StringComparison.OrdinalIgnoreCase);

            // انتهت المدة (تاريخ ماضٍ) رغم بقاء العلم مفعّلًا ← مغلق أيضًا
            await using (var db = RtkRealDb.CreateDb())
                await db.RemedialTrackEnrollments.Where(e => e.Id == s.EnrollmentId)
                    .ExecuteUpdateAsync(x => x.SetProperty(e => e.VideoReviewEnabled, true).SetProperty(e => e.VideoReviewUntilUtc, DateTime.UtcNow.AddMinutes(-5)));
            await GoOkAsync(page, AxisUrl(Review));
            Assert.Equal(0, await page.Locator("#rtkReviewBanner").CountAsync());
            Assert.Equal(0, await page.Locator("#rtkPlayerWrap").CountAsync());
        }

        // ───────────── S11: صفحة ولي الأمر ─────────────

        [SqlServerFact]
        public async Task ParentReportPage_IsRtl_NoHorizontalScroll_ShowsResponsibility_EncodesHtml_AndPrintsClean()
        {
            var page = await LoginAsync(_parentUserName);

            await GoOkAsync(page, "/Parents/RemedialTrackReports");
            await AssertNoHorizontalScrollAsync(page, "قائمة تقارير ولي الأمر");

            await GoOkAsync(page, $"/Parents/RemedialTrackReports/Details?id={_reportId}");
            Assert.Equal("rtl", await page.EvaluateAsync<string>("() => document.documentElement.dir"));
            await AssertNoHorizontalScrollAsync(page, "تفاصيل تقرير ولي الأمر");

            var html = await page.ContentAsync();
            Assert.DoesNotContain(XssName, html);                                                       // لا HTML خام من اللقطة
            var text = await page.InnerTextAsync("body");
            Assert.Contains("مسؤولية", text);                                                           // نص مسؤولية ولي الأمر ظاهر
            Assert.Contains(XssName, text);                                                             // يظهر نصًا لا ينفَّذ

            // الطباعة: عناصر no-print مخفية والاتجاه يبقى RTL
            await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Media.Print });
            var hiddenCount = await page.EvaluateAsync<int>(
                "() => [...document.querySelectorAll('.no-print')].filter(e => getComputedStyle(e).display !== 'none').length");
            Assert.Equal(0, hiddenCount);
            Assert.Equal("rtl", await page.EvaluateAsync<string>("() => getComputedStyle(document.body).direction"));

            // إقرار بالاطلاع: يعمل مرة واحدة ويبقى الزر محميًا بتوكن (الواجهة تُظهر الحالة بعد التحديث)
            await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Media.Screen });
            var ack = page.Locator("form[action*='Acknowledge'] button[type=submit]");
            await Assertions.Expect(ack).ToBeVisibleAsync();
            await ack.ClickAsync();
            await page.WaitForURLAsync(url => url.Contains("/RemedialTrackReports/Details"), new PageWaitForURLOptions { Timeout = 15000 });
            await using var db = RtkRealDb.CreateDb();
            Assert.NotNull(await db.RemedialTrackParentReports.AsNoTracking().Where(r => r.Id == _reportId).Select(r => r.AcknowledgedAtUtc).SingleAsync());
        }
    }
}
