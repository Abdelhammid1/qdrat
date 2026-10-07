using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // RTK-S12.3 — تكامل HTTP لحزمة v2 (Program.cs الحقيقي + قاعدة الاختبار المعزولة):
    // صلاحيات/Antiforgery لكل POST جديد (S9–S12)، IDOR بين أولياء الأمور (404)، إخفاء تقرير غير مُرسل/موقوف،
    // ترميز HTML للقطة (XSS)، وإجراءات الأدمن عبر الـ HTTP. يتخطّى ذاتيًا إن غاب QDRAT_TEST_SQL.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "Integration")]
    public sealed class RemedialTrackV2IntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string ParentBase = "/Parents/RemedialTrackReports";
        private const string XssName = "<script>alert(1)</script>";

        private sealed class RealTz : ITimeZoneService
        {
            public DateTime GetNowUtc() => DateTime.UtcNow;
            public DateTime GetNowSaudi() => DateTime.UtcNow.AddHours(3);
            public DateTime ConvertToSaudi(DateTime utcTime) => utcTime.AddHours(3);
            public DateTime ConvertToUtc(DateTime saudiTime) => saudiTime.AddHours(-3);
        }

        private readonly CustomWebApplicationFactory _factory;
        private RtkSeed _seed = null!;
        private string _parentAUser = "", _parentBUser = "";
        private int _reportA, _reportB, _reportNoParent;

        public RemedialTrackV2IntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            // 3 طلاب: أ (ولي أمر 1، اسمه يحوي HTML)، ب (ولي أمر 2)، ج (بلا ولي أمر)
            _seed = await RtkRealDb.SeedAsync(students: 3, firstAxisStatus: RemedialTrackAxisStatus.AwaitingExam102);

            await using (var db = RtkRealDb.CreateDb())
            {
                db.Users.Add(new QdratNew.Entities.ApplicationUser
                {
                    Id = "rtks7-owner", UserName = "rtks7-owner", NormalizedUserName = "RTKS7-OWNER",
                    Email = "rtks7-owner@test.local", NormalizedEmail = "RTKS7-OWNER@TEST.LOCAL", EmailConfirmed = true,
                    NationalID = "RS7OWNER02", FullName = "مدير RTKS7", SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N"), IsActive = true
                });
                var a = _seed.Students[0].StudentId;
                await db.Students.Where(s => s.StudentID == a)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, XssName));
                await db.SaveChangesAsync();
            }

            (_, _parentAUser) = await RtkRealDb.LinkParentAsync(_seed.Students[0].StudentId, 11);
            (_, _parentBUser) = await RtkRealDb.LinkParentAsync(_seed.Students[1].StudentId, 12);

            var progress = new RemedialTrackProgressService(new RtkRealDb.Factory(), TimeProvider.System, new RealTz(),
                NullLogger<RemedialTrackProgressService>.Instance);
            foreach (var s in _seed.Students)
                await progress.OnExamSubmittedAsync(await RtkRealDb.AddFailedAttempt102Async(_seed, s));

            await using var check = RtkRealDb.CreateDb();
            var reports = await check.RemedialTrackParentReports.AsNoTracking().ToListAsync();
            _reportA = reports.Single(r => r.EnrollmentId == _seed.Students[0].EnrollmentId).Id;
            _reportB = reports.Single(r => r.EnrollmentId == _seed.Students[1].EnrollmentId).Id;
            _reportNoParent = reports.Single(r => r.EnrollmentId == _seed.Students[2].EnrollmentId).Id;
        }

        public Task DisposeAsync() => RtkRealDb.IsConfigured ? RtkRealDb.CleanupAsync() : Task.CompletedTask;

        // ───────────── أدوات ─────────────

        private HttpClient Client(string? userId = null, string? role = null, bool withAntiforgery = false)
        {
            var c = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            if (userId != null)
            {
                c.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
                c.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role ?? "Student");
                if (withAntiforgery)
                {
                    var t = _factory.CreateValidAntiForgeryTokens(userId);
                    c.DefaultRequestHeaders.Add("Cookie", t.CookieHeader);
                    c.DefaultRequestHeaders.Add("RequestVerificationToken", t.RequestToken);
                }
            }
            return c;
        }

        private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
            => new(fields.ToDictionary(f => f.Key, f => f.Value));

        private static string Decoded(string html) => WebUtility.HtmlDecode(html);

        private static bool IsRejected(HttpStatusCode s)
            => s is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found;

        private async Task<RemedialTrackParentReportStatusSnapshot> StateAsync(int reportId)
        {
            await using var db = RtkRealDb.CreateDb();
            var r = await db.RemedialTrackParentReports.AsNoTracking().SingleAsync(x => x.Id == reportId);
            return new RemedialTrackParentReportStatusSnapshot(r.Status, r.AcknowledgedAtUtc);
        }

        private sealed record RemedialTrackParentReportStatusSnapshot(RemedialTrackParentReportStatus Status, DateTime? AcknowledgedAtUtc);

        // ───────────── الأدمن: صلاحيات + Antiforgery للإجراءات الجديدة ─────────────

        private static readonly string[] NewAdminPostUrls =
        {
            "/Admin/RemedialTrackReports/SendParentReport",
            "/Admin/RemedialTrackReports/ResendParentReport",
            "/Admin/RemedialTrackReports/SuppressParentReport",
            "/Admin/RemedialTrackPublications/ToggleAutoSend",
            "/Admin/RemedialTrackPublications/SetVideoReview",
            "/Admin/RemedialTrackPublications/Delete",
            "/Admin/RemedialTrackPublications/Restore"
        };

        [SqlServerFact]
        public async Task EmployeeWithoutPermissions_Gets403_OnAllNewV2AdminEndpoints()
        {
            var c = Client("rtks7-employee-noperm", "Employee", withAntiforgery: true);

            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/Admin/RemedialTrackReports/ParentReports")).StatusCode);

            foreach (var url in NewAdminPostUrls)
            {
                var r = await c.PostAsync(url, Form(("id", _reportA.ToString()), ("PublicationId", _seed.PublicationId.ToString()), ("Enabled", "false")));
                Assert.True(r.StatusCode == HttpStatusCode.Forbidden, $"{url} → {r.StatusCode}");
            }

            Assert.Equal(RemedialTrackParentReportStatus.Sent, (await StateAsync(_reportA)).Status);   // لم يتغيّر شيء
        }

        [SqlServerFact]
        public async Task NewV2AdminPosts_WithoutAntiforgeryToken_AreRejectedWith400()
        {
            var owner = Client("rtks7-owner", "Owner");   // مصرَّح (Owner) لكن بلا توكن ← 400 قبل أي تنفيذ

            foreach (var url in NewAdminPostUrls)
            {
                var r = await owner.PostAsync(url, Form(("id", _reportA.ToString()), ("PublicationId", _seed.PublicationId.ToString()), ("Enabled", "false")));
                Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"{url} → {r.StatusCode}");
            }

            Assert.Equal(RemedialTrackParentReportStatus.Sent, (await StateAsync(_reportA)).Status);
            await using var db = RtkRealDb.CreateDb();
            Assert.True((await db.RemedialTrackPublications.AsNoTracking().SingleAsync(p => p.Id == _seed.PublicationId)).AutoSendParentReports);
        }

        [SqlServerFact]
        public async Task Anonymous_IsRejected_OnParentAndAdminV2Endpoints()
        {
            var c = Client();
            foreach (var url in new[] { ParentBase, $"{ParentBase}/Details?id={_reportA}", "/Admin/RemedialTrackReports/ParentReports" })
            {
                var r = await c.GetAsync(url);
                Assert.True(IsRejected(r.StatusCode), $"{url} → {r.StatusCode}");
            }
        }

        [SqlServerFact]
        public async Task OwnerOnAdminQueue_CanSuppressAndSend_ThroughHttp()
        {
            var owner = Client("rtks7-owner", "Owner", withAntiforgery: true);

            var page = await owner.GetAsync("/Admin/RemedialTrackReports/ParentReports");
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var html = await page.Content.ReadAsStringAsync();
            Assert.DoesNotContain(XssName, html);                       // اسم الطالب يُرمَّز

            var suppress = await owner.PostAsync("/Admin/RemedialTrackReports/SuppressParentReport", Form(("Id", _reportB.ToString())));
            Assert.Equal(HttpStatusCode.OK, suppress.StatusCode);
            Assert.Contains("\"success\":true", await suppress.Content.ReadAsStringAsync());
            Assert.Equal(RemedialTrackParentReportStatus.Suppressed, (await StateAsync(_reportB)).Status);

            // NoParent لا يُرسَل (رسالة فشل عامة بلا استثناء)
            var send = await owner.PostAsync("/Admin/RemedialTrackReports/SendParentReport", Form(("Id", _reportNoParent.ToString())));
            Assert.Equal(HttpStatusCode.OK, send.StatusCode);
            Assert.Contains("\"success\":false", await send.Content.ReadAsStringAsync());
            Assert.Equal(RemedialTrackParentReportStatus.NoParent, (await StateAsync(_reportNoParent)).Status);

            // معرّف غير موجود ≡ خارج النطاق ← 404
            var missing = await owner.PostAsync("/Admin/RemedialTrackReports/SendParentReport", Form(("Id", "99999999")));
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

            // إعادة التقرير إلى «مُرسل» لباقي الاختبارات (اختبارات المجموعة تتشارك الحالة داخل الصنف)
            var resend = await owner.PostAsync("/Admin/RemedialTrackReports/SendParentReport", Form(("Id", _reportB.ToString())));
            Assert.Contains("\"success\":true", await resend.Content.ReadAsStringAsync());
            Assert.Equal(RemedialTrackParentReportStatus.Sent, (await StateAsync(_reportB)).Status);
        }

        // ───────────── ولي الأمر: IDOR + ترميز + إقرار ─────────────

        [SqlServerFact]
        public async Task StudentRole_CannotReachParentReports()
        {
            var c = Client(_seed.Students[0].UserId, "Student", withAntiforgery: true);
            var r = await c.GetAsync($"{ParentBase}/Details?id={_reportA}");
            Assert.True(IsRejected(r.StatusCode), $"→ {r.StatusCode}");
        }

        [SqlServerFact]
        public async Task ParentB_CannotReadOrAcknowledge_ParentAReport_Returns404()
        {
            var b = Client(_parentBUser, "Parent", withAntiforgery: true);

            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"{ParentBase}/Details?id={_reportA}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"{ParentBase}/Acknowledge", Form(("id", _reportA.ToString())))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"{ParentBase}/Details?id=99999999")).StatusCode);   // لا فرق بين غير موجود وغير مملوك
            Assert.Null((await StateAsync(_reportA)).AcknowledgedAtUtc);
        }

        [SqlServerFact]
        public async Task ParentA_SeesOwnReport_EncodedSnapshot_AndAcknowledgesOnce()
        {
            var a = Client(_parentAUser, "Parent", withAntiforgery: true);

            var list = await a.GetAsync(ParentBase);
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.DoesNotContain(XssName, await list.Content.ReadAsStringAsync());

            var details = await a.GetAsync($"{ParentBase}/Details?id={_reportA}");
            var html = await details.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, details.StatusCode);
            Assert.DoesNotContain(XssName, html);                                                  // لا HTML خام من اللقطة
            Assert.Contains("&lt;script&gt;", html);                                                   // ظهر مُرمَّزًا
            Assert.DoesNotContain(RtkRealDb.SecretExplanation, html);                               // لا تسريب لمحتوى الأسئلة

            var ack1 = await a.PostAsync($"{ParentBase}/Acknowledge", Form(("id", _reportA.ToString())));
            Assert.Equal(HttpStatusCode.Redirect, ack1.StatusCode);
            var first = (await StateAsync(_reportA)).AcknowledgedAtUtc;
            Assert.NotNull(first);

            var ack2 = await a.PostAsync($"{ParentBase}/Acknowledge", Form(("id", _reportA.ToString())));
            Assert.Equal(HttpStatusCode.Redirect, ack2.StatusCode);
            Assert.Equal(first, (await StateAsync(_reportA)).AcknowledgedAtUtc);                    // مرة واحدة (Idempotent)
        }

        [SqlServerFact]
        public async Task ParentPost_WithoutAntiforgeryToken_IsRejectedWith400()
        {
            var a = Client(_parentAUser, "Parent", withAntiforgery: false);
            var r = await a.PostAsync($"{ParentBase}/Acknowledge", Form(("id", _reportA.ToString())));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        [SqlServerFact]
        public async Task SuppressedAndPendingReports_AreHiddenFromParent()
        {
            await using (var db = RtkRealDb.CreateDb())
                await db.RemedialTrackParentReports.Where(r => r.Id == _reportB)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RemedialTrackParentReportStatus.Suppressed));
            var b = Client(_parentBUser, "Parent", withAntiforgery: true);
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"{ParentBase}/Details?id={_reportB}")).StatusCode);

            await using (var db = RtkRealDb.CreateDb())
                await db.RemedialTrackParentReports.Where(r => r.Id == _reportB)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RemedialTrackParentReportStatus.Pending));
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"{ParentBase}/Details?id={_reportB}")).StatusCode);

            await using (var db = RtkRealDb.CreateDb())
                await db.RemedialTrackParentReports.Where(r => r.Id == _reportB)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RemedialTrackParentReportStatus.Sent));
            Assert.Equal(HttpStatusCode.OK, (await b.GetAsync($"{ParentBase}/Details?id={_reportB}")).StatusCode);
        }
    }
}
