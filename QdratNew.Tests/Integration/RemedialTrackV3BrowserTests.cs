using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Moq;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // RTK-S13.5 — رحلة الطالب على 375px بمتصفح حقيقي وقاعدة الاختبار المعزولة:
    // «مطلوب إضافي» في الخطة (حتى لو المحور Passed) ← صفحة الملحق (مشغّل بـ data-mode="addendum") ← اختبار الملحق ← النتيجة،
    // ويبقى المحور Passed والمحور التالي مفتوحًا قبل الملحق وبعده. + IDOR وAntiforgery على Endpoint النبضة.
    // لا يُشغَّل فيديو حقيقي (يوتيوب شبكة خارجية): اكتمال الفيديو يُهيَّأ في القاعدة كما في بقية اختبارات E2E. المنفذ 5301.
    // ملاحظة: الخادم الفرعي يُشغَّل من bin/Debug أو من QDRAT_E2E_DLL (انظر RealServerFixture).
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "E2E")]
    public sealed class RemedialTrackV3BrowserTests : IAsyncLifetime
    {
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
        private int _addendumId;

        private const int Target = 0, Other = 1;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            _seed = await RtkRealDb.SeedAsync(students: 2, withLogin: true, firstAxisStatus: RemedialTrackAxisStatus.Passed);

            // المحور التالي مفتوح (Videos) قبل الملحق
            await using (var db = RtkRealDb.CreateDb())
                foreach (var s in _seed.Students)
                    await db.RemedialTrackAxisProgresses.Where(a => a.Id == s.AxisProgressIds[1])
                        .ExecuteUpdateAsync(x => x.SetProperty(a => a.Status, RemedialTrackAxisStatus.Videos));

            var admin = new RemedialTrackAddendumService(new RtkRealDb.Factory(), TimeProvider.System,
                new Mock<INotificationService>().Object, new Mock<IAdminActivityLogger>().Object, NullLogger<RemedialTrackAddendumService>.Instance);
            var r = await admin.CreateAsync(
                new CreateAddendumInput(_seed.PublicationId, _seed.AxisIds[0], "RTKS7 ملحق فيديو", "https://youtu.be/ddddddddddd", 100,
                    "الفيديو الأصلي أُهمل عند إنشاء الخطة", _seed.Model101Id, 15, false, new[] { _seed.Students[Target].EnrollmentId }),
                new RemedialTrackActor("rtks7-admin", "مدير"), RemedialTrackBatchScope.Unrestricted);
            Assert.True(r.Success, r.Message);
            _addendumId = (int)r.Data!;

            _server = await RealServerFixture.StartAsync(5301);
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

        private async Task<IPage> LoginAsync(string userName)
        {
            var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                BaseURL = _server!.BaseUrl,
                ViewportSize = new ViewportSize { Width = 375, Height = 812 },
                IsMobile = true,
                HasTouch = true
            });
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
            Assert.True(overflow <= 1, $"تمرير أفقي في «{where}» على 375px (زيادة {overflow}px)");
        }

        [SqlServerFact]
        public async Task StudentJourney_OnMobile375_Plan_Addendum_Watch_Exam_Result_AxisStaysPassed()
        {
            var s = _seed.Students[Target];
            var page = await LoginAsync(s.UserName);

            // ---------- الخطة: البطاقة تظهر والمحور Passed والتالي مفتوح ----------
            await GoOkAsync(page, $"/Students/RemedialTrack/Open?enrollmentId={s.EnrollmentId}");
            Assert.Equal("rtl", await page.EvaluateAsync<string>("() => document.documentElement.dir"));
            var card = page.Locator(".rtk-addendum");
            await Assertions.Expect(card).ToHaveCountAsync(1);
            await Assertions.Expect(card).ToContainTextAsync("مطلوب إضافي");
            await Assertions.Expect(card).ToContainTextAsync("لا يمنع التقدّم");
            await Assertions.Expect(page.Locator(".rtk-axis.is-passed")).ToHaveCountAsync(1);       // المحور 1 Passed ويظهر معه الملحق
            await Assertions.Expect(page.Locator(".rtk-axis.is-current")).ToHaveCountAsync(1);      // المحور 2 مفتوح قبل الملحق
            await AssertNoHorizontalScrollAsync(page, "صفحة الخطة بالبطاقة");

            // ---------- صفحة الملحق: المشغّل بوضع الملحق ----------
            var open = card.Locator("a.rtk-btn");
            Assert.True((await open.BoundingBoxAsync())!.Height >= 44, "زر الملحق أقل من 44px");
            await open.ClickAsync();
            await page.WaitForURLAsync(url => url.Contains("/RemedialTrack/Addendum"), new PageWaitForURLOptions { Timeout = 20000 });
            var root = page.Locator("#rtkRoot");
            await Assertions.Expect(root).ToHaveAttributeAsync("data-mode", "addendum");
            Assert.Contains("AddendumPing", await root.GetAttributeAsync("data-ping-url"));
            await Assertions.Expect(page.Locator("#rtkVideoList > li")).ToHaveCountAsync(1);
            await Assertions.Expect(page.Locator(".rtk-watermark")).ToHaveCountAsync(2);            // D22: العلامة المائية باقية
            await Assertions.Expect(page.Locator("#rtkBtnFull")).ToHaveCountAsync(1);               // وملء الشاشة
            await Assertions.Expect(page.Locator(".ax-banner")).ToContainTextAsync("سبب الإضافة");
            await AssertNoHorizontalScrollAsync(page, "صفحة الملحق");
            await Assertions.Expect(page.Locator("form[action*='StartAddendumExam']")).ToHaveCountAsync(0);   // لا اختبار قبل اكتمال الفيديو

            // ---------- Antiforgery وIDOR على Endpoint النبضة ----------
            var pid = await GetProgressIdAsync(s.EnrollmentId);
            var noToken = await page.EvaluateAsync<int>(@"async ([id, enr]) => {
                const r = await fetch('/Students/RemedialTrack/AddendumPing', { method: 'POST', credentials: 'same-origin',
                    headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ enrollmentId: enr, addendumProgressId: id, state: 'playing', duration: 100 }) });
                return r.status; }", new object[] { pid, s.EnrollmentId });
            Assert.Equal(400, noToken);   // بلا رمز التحقق ← رفض

            // ---------- إتمام الفيديو (يُهيَّأ في القاعدة) ← يظهر الاختبار ولا يُسلَّم الرابط ----------
            await using (var db = RtkRealDb.CreateDb())
                await db.RemedialTrackAddendumProgresses.Where(p => p.Id == pid)
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.VideoCompleted, true).SetProperty(p => p.WatchedSeconds, 100d)
                        .SetProperty(p => p.VideoCompletedAtUtc, DateTime.UtcNow));
            await GoOkAsync(page, $"/Students/RemedialTrack/Addendum?enrollmentId={s.EnrollmentId}&addendumId={_addendumId}");
            Assert.DoesNotContain("ddddddddddd", await page.ContentAsync());                        // لا يغادر الخادمَ رابط بعد الاكتمال
            var startBtn = page.Locator("form[action*='StartAddendumExam'] button[type=submit]");
            await Assertions.Expect(startBtn).ToBeVisibleAsync(new() { Timeout = 15000 });
            Assert.True((await startBtn.BoundingBoxAsync())!.Height >= 44, "زر بدء الاختبار أقل من 44px");

            // ---------- الحل ----------
            await startBtn.ClickAsync();
            await page.WaitForURLAsync(url => url.Contains("/RemedialTrack/Solve"), new PageWaitForURLOptions { Timeout = 20000 });
            var sections = page.Locator("section.rtk-q");
            await Assertions.Expect(sections).ToHaveCountAsync(4, new() { Timeout = 15000 });
            await AssertNoHorizontalScrollAsync(page, "واجهة حل الملحق");
            var solveHtml = await page.ContentAsync();
            Assert.DoesNotContain(RtkRealDb.SecretExplanation, solveHtml);                          // D15 لا تسريب
            Assert.DoesNotContain(RtkRealDb.SecretVideoUrl, solveHtml);
            var attemptUrl = page.Url;

            for (var i = 0; i < 4; i++)
            {
                var section = sections.Nth(i);
                await section.Locator("label.qx-option", new() { HasText = "الجواب الصحيح" }).ClickAsync();
                await Assertions.Expect(section.Locator("[data-role=saved]")).ToBeVisibleAsync(new() { Timeout = 15000 });
                if (i < 3) await page.ClickAsync("[data-rtk-nav=next]");
                else await page.ClickAsync("[data-rtk-nav=showReview]");
            }
            await page.ClickAsync("#rtkSubmitBtn");
            try { await page.ClickAsync(".swal2-confirm", new PageClickOptions { Timeout = 6000 }); }
            catch (TimeoutException) { /* لا نافذة تأكيد */ }
            await page.WaitForURLAsync(url => url.Contains("/RemedialTrack/Result"), new PageWaitForURLOptions { Timeout = 20000 });
            await Assertions.Expect(page.Locator(".rs-message h2")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("اختبار الملحق");
            await Assertions.Expect(page.Locator(".rs-next")).ToContainTextAsync("لا يؤثر على نتيجة المحور");
            await AssertNoHorizontalScrollAsync(page, "نتيجة الملحق");

            // العودة إلى الملحق: يظهر الاجتياز والإكمال
            await page.Locator(".rs-next-actions a[href*='/Addendum']").ClickAsync();
            await page.WaitForURLAsync(url => url.Contains("/RemedialTrack/Addendum"), new PageWaitForURLOptions { Timeout = 20000 });
            await Assertions.Expect(page.Locator(".ax-banner-ok").First).ToBeVisibleAsync();

            // ---------- النهاية: المحور Passed والتالي Videos بلا أي انتقال، والملحق مكتمل ----------
            await using (var check = RtkRealDb.CreateDb())
            {
                var aps = await check.RemedialTrackAxisProgresses.AsNoTracking().Where(a => a.EnrollmentId == s.EnrollmentId).OrderBy(a => a.Order).ToListAsync();
                Assert.Equal(RemedialTrackAxisStatus.Passed, aps[0].Status);
                Assert.Equal(RemedialTrackAxisStatus.Videos, aps[1].Status);
                Assert.Null(aps[0].Exam101Percent);
                Assert.Null(aps[0].Exam102Percent);
                var p = await check.RemedialTrackAddendumProgresses.AsNoTracking().SingleAsync(x => x.Id == pid);
                Assert.True(p.ExamPassed);
                Assert.NotNull(p.CompletedAtUtc);
                Assert.Equal(1, p.AttemptsCount);
                Assert.Equal(0, await check.RemedialTrackEvents.CountAsync(e => e.EnrollmentId == s.EnrollmentId && (e.Type == RemedialTrackEventType.ExamPassed || e.Type == RemedialTrackEventType.ExamFailed)));
            }

            await GoOkAsync(page, $"/Students/RemedialTrack/Open?enrollmentId={s.EnrollmentId}");
            await Assertions.Expect(page.Locator(".rtk-axis.is-passed")).ToHaveCountAsync(1);
            await Assertions.Expect(page.Locator(".rtk-addendum.is-completed")).ToHaveCountAsync(1);

            // ---------- IDOR: الطالب غير المستهدف (وفي نفس الأمر) ----------
            var other = _seed.Students[Other];
            var page2 = await LoginAsync(other.UserName);
            await GoOkAsync(page2, $"/Students/RemedialTrack/Open?enrollmentId={other.EnrollmentId}");
            await Assertions.Expect(page2.Locator(".rtk-addendum")).ToHaveCountAsync(0);

            var r1 = await page2.GotoAsync($"/Students/RemedialTrack/Addendum?enrollmentId={other.EnrollmentId}&addendumId={_addendumId}");
            Assert.Equal(404, r1!.Status);
            var r2 = await page2.GotoAsync($"/Students/RemedialTrack/Addendum?enrollmentId={s.EnrollmentId}&addendumId={_addendumId}");
            Assert.Equal(404, r2!.Status);
            var r3 = await page2.GotoAsync(attemptUrl);   // محاولة طالب آخر
            Assert.Equal(404, r3!.Status);
        }

        private async Task<int> GetProgressIdAsync(int enrollmentId)
        {
            await using var db = RtkRealDb.CreateDb();
            return await db.RemedialTrackAddendumProgresses.AsNoTracking()
                .Where(p => p.AddendumId == _addendumId && p.EnrollmentId == enrollmentId).Select(p => p.Id).SingleAsync();
        }
    }
}
