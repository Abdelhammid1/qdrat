using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Enums;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // RTK-S7.2 — تكامل HTTP كامل (Program.cs الحقيقي + قاعدة الاختبار المعزولة) لرحلة الطالب والأدمن:
    // IDOR، Antiforgery، تسريب الإجابات، Rate limiting (429)، الصلاحيات (403)، مفتاح التعطيل، خصوصية تقرير ولي الأمر.
    // إتمام الفيديو الفعلي لا يُختبر عبر مشغّل يوتيوب (شبكة خارجية): تُستدعى VideoPing مباشرة، والإتمام يُهيَّأ في القاعدة.
    // يتخطّى ذاتيًا إن غاب QDRAT_TEST_SQL.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "Integration")]
    public sealed class RemedialTrackIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Base = "/Students/RemedialTrack";

        private readonly CustomWebApplicationFactory _factory;
        private RtkSeed _seed = null!;

        // فهارس الطلاب المزروعين — لكلٍّ سيناريو طالب خاص كي لا تتداخل الحالات
        private const int A = 0, B = 1, Journey = 2, RateCode = 3, RatePing = 4, Kill = 5, Misc = 6;

        public RemedialTrackIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            _seed = await RtkRealDb.SeedAsync(students: 7, inPerson: true);

            // الرقم المرجعي مُتحقَّق مسبقًا لكل الطلاب عدا الرحلة (تختبر البوابة) وطالب Rate-limit الرقم
            await using var db = RtkRealDb.CreateDb();

            // مستخدم أدمن حقيقي (Owner بالترويسة) كي تعمل مسارات تقرأ UserManager مثل SaveNote
            db.Users.Add(new QdratNew.Entities.ApplicationUser
            {
                Id = "rtks7-owner", UserName = "rtks7-owner", NormalizedUserName = "RTKS7-OWNER",
                Email = "rtks7-owner@test.local", NormalizedEmail = "RTKS7-OWNER@TEST.LOCAL", EmailConfirmed = true,
                NationalID = "RS7OWNER01", FullName = "مدير RTKS7", SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"), IsActive = true
            });
            await db.SaveChangesAsync();

            foreach (var i in new[] { A, B, RatePing, Kill, Misc })
            {
                var id = _seed.Students[i].EnrollmentId;
                await db.RemedialTrackEnrollments.Where(e => e.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.VerifiedCodeVersion, 1));
            }
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

        private HttpClient StudentClient(int index, bool withAntiforgery = true)
            => Client(_seed.Students[index].UserId, "Student", withAntiforgery);

        private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
            => new(fields.ToDictionary(f => f.Key, f => f.Value));

        private static StringContent Json(object body)
            => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        private static bool IsRejected(HttpStatusCode s)
            => s is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found;

        // Razor يرمّز الحروف العربية (&#x...;) فنفك الترميز قبل البحث عن نص عربي
        private static string Decoded(string html) => WebUtility.HtmlDecode(html);

        private static string Short(string body) => body.Length > 600 ? body[..600] : body;

        private async Task SetAxisAsync(int studentIndex, RemedialTrackAxisStatus status)
        {
            var apId = _seed.Students[studentIndex].AxisProgressIds[0];
            await using var db = RtkRealDb.CreateDb();
            await db.RemedialTrackAxisProgresses.Where(a => a.Id == apId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, status));
        }

        private async Task CompleteVideosAsync(int studentIndex)
        {
            var apId = _seed.Students[studentIndex].AxisProgressIds[0];
            await using var db = RtkRealDb.CreateDb();
            await db.RemedialTrackVideoProgresses.Where(v => v.AxisProgressId == apId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(v => v.IsCompleted, true)
                    .SetProperty(v => v.EndedSeen, true)
                    .SetProperty(v => v.WatchedSeconds, 100d)
                    .SetProperty(v => v.CompletedAtUtc, DateTime.UtcNow));
        }

        // ───────────── IDOR: طالب ب لا يرى ولا يكتب في بيانات طالب أ ─────────────

        [SqlServerFact]
        public async Task Idor_StudentB_CannotReadOrWriteStudentAData_AllReturn404()
        {
            var a = _seed.Students[A];
            var b = StudentClient(B);

            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"{Base}/Open?enrollmentId={a.EnrollmentId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"{Base}/Axis?enrollmentId={a.EnrollmentId}&axisProgressId={a.AxisProgressIds[0]}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"{Base}/Report?enrollmentId={a.EnrollmentId}")).StatusCode);

            // بدء اختبار على محور طالب أ
            var start = await b.PostAsync($"{Base}/StartExam",
                Form(("enrollmentId", a.EnrollmentId.ToString()), ("axisProgressId", a.AxisProgressIds[0].ToString())));
            Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);

            // نبضة فيديو طالب أ (بمعرّف تسجيله وبمعرّف تسجيل ب)
            foreach (var enrollmentId in new[] { a.EnrollmentId, _seed.Students[B].EnrollmentId })
            {
                var ping = await b.PostAsync($"{Base}/VideoPing", Json(new
                {
                    EnrollmentId = enrollmentId, VideoProgressId = a.FirstAxisVideoProgressIds[0], State = "playing", Position = 1, Duration = 100
                }));
                Assert.Equal(HttpStatusCode.NotFound, ping.StatusCode);
            }

            // لم يُكتب شيء لطالب أ
            await using var db = RtkRealDb.CreateDb();
            var vp = await db.RemedialTrackVideoProgresses.AsNoTracking().SingleAsync(v => v.Id == a.FirstAxisVideoProgressIds[0]);
            Assert.Null(vp.LastPingAtUtc);
            Assert.False(await db.RemedialTrackExamAttempts.AnyAsync(x => x.AxisProgressId == a.AxisProgressIds[0]));
        }

        [SqlServerFact]
        public async Task Idor_AttemptOfAnotherStudent_Returns404_ForSolveSaveSubmitResult()
        {
            // طالب أ يبدأ محاولة حقيقية، ثم يحاول ب الوصول لها
            var a = _seed.Students[Misc];
            await SetAxisAsync(Misc, RemedialTrackAxisStatus.AwaitingExam101);
            var aClient = StudentClient(Misc);
            var start = await aClient.PostAsync($"{Base}/StartExam",
                Form(("enrollmentId", a.EnrollmentId.ToString()), ("axisProgressId", a.AxisProgressIds[0].ToString())));
            Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
            var attemptId = int.Parse(start.Headers.Location!.ToString().Split("attemptId=")[1]);

            Guid questionId;
            await using (var db = RtkRealDb.CreateDb())
                questionId = await db.RemedialTrackExamAttemptQuestions.AsNoTracking()
                    .Where(x => x.AttemptId == attemptId).Select(x => x.QuestionId).FirstAsync();

            var b = StudentClient(B);
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"{Base}/Solve?attemptId={attemptId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"{Base}/Result?attemptId={attemptId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"{Base}/Submit", Form(("attemptId", attemptId.ToString())))).StatusCode);

            var save = await b.PostAsync($"{Base}/SaveAnswer", Json(new { AttemptId = attemptId, QuestionId = questionId, Answer = RtkRealDb.CorrectAnswer }));
            Assert.Equal(HttpStatusCode.NotFound, save.StatusCode);

            await using var check = RtkRealDb.CreateDb();
            Assert.Equal(RemedialTrackAttemptStatus.InProgress,
                (await check.RemedialTrackExamAttempts.AsNoTracking().SingleAsync(x => x.Id == attemptId)).Status);
            Assert.Null((await check.RemedialTrackExamAttemptQuestions.AsNoTracking().FirstAsync(x => x.AttemptId == attemptId && x.QuestionId == questionId)).SelectedAnswer);
        }

        // ───────────── المصادقة والصلاحيات ─────────────

        [SqlServerFact]
        public async Task Anonymous_IsRejected_OnStudentAndAdminEndpoints()
        {
            var c = Client();
            foreach (var url in new[]
            {
                Base, $"{Base}/Open?enrollmentId=1", "/Admin/RemedialTracks", "/Admin/RemedialTrackPublications", "/Admin/RemedialTrackReports/Student?enrollmentId=1"
            })
            {
                var r = await c.GetAsync(url);
                Assert.True(IsRejected(r.StatusCode), $"{url} → {r.StatusCode}");
            }
        }

        [SqlServerFact]
        public async Task StudentRole_CannotReachAdminEndpoints()
        {
            var c = StudentClient(A);
            foreach (var url in new[] { "/Admin/RemedialTracks", "/Admin/RemedialTrackPublications", $"/Admin/RemedialTrackReports/Student?enrollmentId={_seed.Students[A].EnrollmentId}" })
            {
                var r = await c.GetAsync(url);
                Assert.True(IsRejected(r.StatusCode), $"{url} → {r.StatusCode}");
            }
        }

        [SqlServerFact]
        public async Task EmployeeWithoutPermissions_Gets403_OnAllRtkAdminEndpoints()
        {
            var c = Client("rtks7-employee-noperm", "Employee", withAntiforgery: true);
            var enrollmentId = _seed.Students[A].EnrollmentId;

            foreach (var url in new[]
            {
                "/Admin/RemedialTracks", "/Admin/RemedialTracks/Create", "/Admin/RemedialTrackPublications", "/Admin/RemedialTrackPublications/Create",
                $"/Admin/RemedialTrackPublications/Details?id={_seed.PublicationId}",
                $"/Admin/RemedialTrackReports/Student?enrollmentId={enrollmentId}", $"/Admin/RemedialTrackReports/Batch?publicationId={_seed.PublicationId}"
            })
                Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(url)).StatusCode);

            foreach (var (url, form) in new (string, FormUrlEncodedContent)[]
            {
                ("/Admin/RemedialTrackPublications/Cancel", Form(("id", _seed.PublicationId.ToString()))),
                ("/Admin/RemedialTrackPublications/RegenerateCode", Form(("id", _seed.PublicationId.ToString()))),
                ("/Admin/RemedialTrackReports/SaveNote", Form(("EnrollmentId", enrollmentId.ToString()), ("Note", "x"))),
                ("/Admin/RemedialTracks/Archive", Form(("id", _seed.TrackId.ToString())))
            })
                Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync(url, form)).StatusCode);

            // ولم يتغيّر شيء
            await using var db = RtkRealDb.CreateDb();
            Assert.Equal(RemedialTrackPublicationStatus.Active, (await db.RemedialTrackPublications.AsNoTracking().SingleAsync(p => p.Id == _seed.PublicationId)).Status);
            Assert.Equal(RemedialTrackStatus.Ready, (await db.RemedialTracks.AsNoTracking().SingleAsync(t => t.Id == _seed.TrackId)).Status);
        }

        // ───────────── Antiforgery ─────────────

        [SqlServerFact]
        public async Task Posts_WithoutAntiforgeryToken_AreRejectedWith400()
        {
            var s = _seed.Students[A];
            var student = StudentClient(A, withAntiforgery: false);

            Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsync($"{Base}/VerifyCode",
                Form(("EnrollmentId", s.EnrollmentId.ToString()), ("Code", "123456")))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsync($"{Base}/VideoPing", Json(new
                { EnrollmentId = s.EnrollmentId, VideoProgressId = s.FirstAxisVideoProgressIds[0], State = "playing", Position = 1, Duration = 100 }))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsync($"{Base}/StartExam",
                Form(("enrollmentId", s.EnrollmentId.ToString()), ("axisProgressId", s.AxisProgressIds[0].ToString())))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsync($"{Base}/Submit", Form(("attemptId", "1")))).StatusCode);

            var owner = Client("rtks7-owner", "Owner");
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/Admin/RemedialTrackPublications/Cancel", Form(("id", _seed.PublicationId.ToString())))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/Admin/RemedialTrackPublications/Unlock", Form(("EnrollmentId", s.EnrollmentId.ToString())))).StatusCode);

            await using var db = RtkRealDb.CreateDb();
            Assert.Equal(RemedialTrackPublicationStatus.Active, (await db.RemedialTrackPublications.AsNoTracking().SingleAsync(p => p.Id == _seed.PublicationId)).Status);
        }

        // ───────────── Rate limiting (429 فعلي) ─────────────

        [SqlServerFact]
        public async Task VerifyCode_IsRateLimited_AndAnswersAreGenericWithoutEnumeration()
        {
            var s = _seed.Students[RateCode];
            var c = StudentClient(RateCode);
            var statuses = new List<HttpStatusCode>();
            var bodies = new List<string>();

            for (var i = 0; i < 11; i++)
            {
                var r = await c.PostAsync($"{Base}/VerifyCode", Form(("EnrollmentId", s.EnrollmentId.ToString()), ("Code", "000000")));
                statuses.Add(r.StatusCode);
                bodies.Add(await r.Content.ReadAsStringAsync());
            }

            Assert.All(statuses.Take(10), st => Assert.Equal(HttpStatusCode.OK, st));          // صفحة البوابة (خطأ عام ثم قفل)
            Assert.Equal(HttpStatusCode.TooManyRequests, statuses[10]);
            Assert.DoesNotContain(_seed.AccessCode!, string.Concat(bodies));                    // لا يتسرب الرقم الصحيح
        }

        [SqlServerFact]
        public async Task VideoPing_IsRateLimited_At21stRequestPerMinute()
        {
            var s = _seed.Students[RatePing];
            var c = StudentClient(RatePing);
            var statuses = new List<HttpStatusCode>();

            for (var i = 0; i < 21; i++)
            {
                var r = await c.PostAsync($"{Base}/VideoPing", Json(new
                    { EnrollmentId = s.EnrollmentId, VideoProgressId = s.FirstAxisVideoProgressIds[0], State = "playing", Position = i, Duration = 100 }));
                statuses.Add(r.StatusCode);
            }

            Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(20));
            Assert.Equal(HttpStatusCode.TooManyRequests, statuses[20]);
        }

        // ───────────── الرحلة الكاملة + عدم تسريب الإجابات ─────────────

        [SqlServerFact]
        public async Task StudentJourney_Gate_Videos_Exam_Submit_Result_NoAnswerLeak_DoubleSubmitIdempotent()
        {
            var s = _seed.Students[Journey];
            var c = StudentClient(Journey);
            var secrets = new[] { RtkRealDb.SecretExplanation, RtkRealDb.SecretVideoUrl, _seed.AccessCode! };

            // 1) قائمة الخطة
            var index = await c.GetAsync(Base);
            Assert.Equal(HttpStatusCode.OK, index.StatusCode);
            Assert.Contains(RtkRealDb.Marker, await index.Content.ReadAsStringAsync());

            // 2) بوابة الرقم (حضوري): الصفحة تُعرض ولا تكشف الرقم
            var gate = await c.GetAsync($"{Base}/Open?enrollmentId={s.EnrollmentId}");
            Assert.Equal(HttpStatusCode.OK, gate.StatusCode);
            Assert.DoesNotContain(_seed.AccessCode!, await gate.Content.ReadAsStringAsync());

            // نبضة قبل التحقق = بوابة (لا رصيد)
            var pre = await c.PostAsync($"{Base}/VideoPing", Json(new
                { EnrollmentId = s.EnrollmentId, VideoProgressId = s.FirstAxisVideoProgressIds[0], State = "playing", Position = 1, Duration = 100 }));
            Assert.NotEqual(HttpStatusCode.OK, pre.StatusCode);

            // 3) رقم خاطئ ← 200 (بوابة + رسالة عامة)، صحيح ← 302 إلى Open
            var wrong = await c.PostAsync($"{Base}/VerifyCode", Form(("EnrollmentId", s.EnrollmentId.ToString()), ("Code", _seed.AccessCode == "111111" ? "222222" : "111111")));
            Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
            var ok = await c.PostAsync($"{Base}/VerifyCode", Form(("EnrollmentId", s.EnrollmentId.ToString()), ("Code", _seed.AccessCode!)));
            Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);

            // 4) صفحة الخطة والمحور (بدون أسرار)
            var open = await c.GetAsync($"{Base}/Open?enrollmentId={s.EnrollmentId}");
            Assert.Equal(HttpStatusCode.OK, open.StatusCode);
            var axis = await c.GetAsync($"{Base}/Axis?enrollmentId={s.EnrollmentId}&axisProgressId={s.AxisProgressIds[0]}");
            var axisHtml = await axis.Content.ReadAsStringAsync();
            Assert.True(axis.StatusCode == HttpStatusCode.OK, Short(axisHtml));
            Assert.Contains($"{RtkRealDb.Marker} فيديو 1-1", Decoded(axisHtml));
            Assert.All(secrets, secret => Assert.DoesNotContain(secret, axisHtml));

            // 5) نبضة صالحة بعد التحقق + فيديو ثانٍ مقفل قبل إتمام الأول (409)
            var ping = await c.PostAsync($"{Base}/VideoPing", Json(new
                { EnrollmentId = s.EnrollmentId, VideoProgressId = s.FirstAxisVideoProgressIds[0], State = "playing", Position = 1, Duration = 100 }));
            Assert.Equal(HttpStatusCode.OK, ping.StatusCode);
            var locked = await c.PostAsync($"{Base}/VideoPing", Json(new
                { EnrollmentId = s.EnrollmentId, VideoProgressId = s.FirstAxisVideoProgressIds[1], State = "playing", Position = 1, Duration = 100 }));
            Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);

            // اختبار قبل إتمام الفيديوهات مرفوض (لا يُنشأ محاولة)
            var early = await c.PostAsync($"{Base}/StartExam",
                Form(("enrollmentId", s.EnrollmentId.ToString()), ("axisProgressId", s.AxisProgressIds[0].ToString())));
            Assert.Equal(HttpStatusCode.Redirect, early.StatusCode);                      // رسالة وعودة لصفحة المحور
            Assert.Contains("Axis", early.Headers.Location!.ToString());
            await using (var db = RtkRealDb.CreateDb())
                Assert.False(await db.RemedialTrackExamAttempts.AnyAsync(a => a.AxisProgressId == s.AxisProgressIds[0]));

            // 6) الإتمام يُهيَّأ في القاعدة (لا مشغّل يوتيوب في الاختبارات)
            await CompleteVideosAsync(Journey);
            await SetAxisAsync(Journey, RemedialTrackAxisStatus.AwaitingExam101);

            // 7) بدء الاختبار ← Solve (بلا CorrectAnswer/Explanation/VideoUrl)
            var start = await c.PostAsync($"{Base}/StartExam",
                Form(("enrollmentId", s.EnrollmentId.ToString()), ("axisProgressId", s.AxisProgressIds[0].ToString())));
            Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
            var solveUrl = start.Headers.Location!.ToString();
            Assert.Contains("Solve", solveUrl);
            var attemptId = int.Parse(solveUrl.Split("attemptId=")[1]);

            var solve = await c.GetAsync($"{Base}/Solve?attemptId={attemptId}");
            var solveHtml = await solve.Content.ReadAsStringAsync();
            Assert.True(solve.StatusCode == HttpStatusCode.OK, Short(solveHtml));
            Assert.All(secrets, secret => Assert.DoesNotContain(secret, solveHtml));
            Assert.DoesNotContain("correctAnswer", solveHtml, StringComparison.OrdinalIgnoreCase);

            // النتيجة قبل التسليم تُحيل لصفحة الحل
            var earlyResult = await c.GetAsync($"{Base}/Result?attemptId={attemptId}");
            Assert.Equal(HttpStatusCode.Redirect, earlyResult.StatusCode);
            Assert.Contains("Solve", earlyResult.Headers.Location!.ToString());

            // 8) حفظ الإجابات (JSON): لا تسريب في الاستجابة
            List<Guid> qids;
            await using (var db = RtkRealDb.CreateDb())
                qids = await db.RemedialTrackExamAttemptQuestions.AsNoTracking()
                    .Where(x => x.AttemptId == attemptId).OrderBy(x => x.Order).Select(x => x.QuestionId).ToListAsync();
            Assert.Equal(4, qids.Count);
            foreach (var q in qids)
            {
                var save = await c.PostAsync($"{Base}/SaveAnswer", Json(new { AttemptId = attemptId, QuestionId = q, Answer = RtkRealDb.CorrectAnswer }));
                var saveBody = await save.Content.ReadAsStringAsync();
                Assert.True(save.StatusCode == HttpStatusCode.OK, saveBody);
                Assert.All(secrets, secret => Assert.DoesNotContain(secret, saveBody));
                Assert.DoesNotContain("correct", saveBody, StringComparison.OrdinalIgnoreCase);
            }

            // 9) تسليم مزدوج متتابع + متزامن: Idempotent
            var s1 = await c.PostAsync($"{Base}/Submit", Form(("attemptId", attemptId.ToString())));
            var s2 = await c.PostAsync($"{Base}/Submit", Form(("attemptId", attemptId.ToString())));
            Assert.Equal(HttpStatusCode.Redirect, s1.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, s2.StatusCode);
            Assert.Contains("Result", s1.Headers.Location!.ToString());
            Assert.Contains("Result", s2.Headers.Location!.ToString());

            var result = await c.GetAsync($"{Base}/Result?attemptId={attemptId}");
            var resultHtml = await result.Content.ReadAsStringAsync();
            Assert.True(result.StatusCode == HttpStatusCode.OK, Short(resultHtml));
            Assert.DoesNotContain(RtkRealDb.SecretVideoUrl, resultHtml);
            Assert.DoesNotContain(_seed.AccessCode!, resultHtml);

            await using (var db = RtkRealDb.CreateDb())
            {
                var attempts = await db.RemedialTrackExamAttempts.AsNoTracking().Where(a => a.AxisProgressId == s.AxisProgressIds[0]).ToListAsync();
                Assert.Single(attempts);
                Assert.True(attempts[0].IsPassed);
                var aps = await db.RemedialTrackAxisProgresses.AsNoTracking().Where(a => a.EnrollmentId == s.EnrollmentId).OrderBy(a => a.Order).ToListAsync();
                Assert.Equal(RemedialTrackAxisStatus.Passed, aps[0].Status);
                Assert.Equal(RemedialTrackAxisStatus.Videos, aps[1].Status);               // فُتح المحور التالي
                Assert.Equal(1, await db.RemedialTrackEvents.CountAsync(e => e.EnrollmentId == s.EnrollmentId && e.Type == RemedialTrackEventType.ExamPassed));
            }

            // 10) تقرير الطالب النهائي (مالك فقط) يُعرض
            var report = await c.GetAsync($"{Base}/Report?enrollmentId={s.EnrollmentId}");
            Assert.True(report.StatusCode == HttpStatusCode.OK, Short(await report.Content.ReadAsStringAsync()));
        }

        // ───────────── خصوصية تقرير ولي الأمر ─────────────

        [SqlServerFact]
        public async Task ParentAndBatchReports_AreOwnerReachable_AndNeverLeakCodeOrOtherStudents()
        {
            var owner = Client("rtks7-owner", "Owner");
            var a = _seed.Students[A];

            var student = await owner.GetAsync($"/Admin/RemedialTrackReports/Student?enrollmentId={a.EnrollmentId}");
            var studentHtml = await student.Content.ReadAsStringAsync();
            Assert.True(student.StatusCode == HttpStatusCode.OK, Short(studentHtml));
            Assert.Contains($"{RtkRealDb.Marker} 1", Decoded(studentHtml));                       // اسم الطالب نفسه
            Assert.DoesNotContain(_seed.AccessCode!, studentHtml);
            Assert.DoesNotContain($"طالب {RtkRealDb.Marker} 2", Decoded(studentHtml));            // لا بيانات طالب آخر (ب)
            Assert.DoesNotContain(RtkRealDb.SecretExplanation, studentHtml);

            var batch = await owner.GetAsync($"/Admin/RemedialTrackReports/Batch?publicationId={_seed.PublicationId}");
            var batchHtml = await batch.Content.ReadAsStringAsync();
            Assert.True(batch.StatusCode == HttpStatusCode.OK, Short(batchHtml));
            Assert.DoesNotContain(_seed.AccessCode!, batchHtml);

            // الرقم المرجعي يراه الأدمن في تفاصيل أمر النشر فقط (لا في التقارير)
            var details = await owner.GetAsync($"/Admin/RemedialTrackPublications/Details?id={_seed.PublicationId}");
            Assert.True(details.StatusCode == HttpStatusCode.OK, Short(await details.Content.ReadAsStringAsync()));
        }

        [SqlServerFact]
        public async Task XssPayloadInAdminNote_IsRenderedEncoded()
        {
            const string payload = "<script>alert('rtks7')</script>";
            var owner = Client("rtks7-owner", "Owner", withAntiforgery: true);
            var a = _seed.Students[A];

            var save = await owner.PostAsync("/Admin/RemedialTrackReports/SaveNote",
                Form(("EnrollmentId", a.EnrollmentId.ToString()), ("Note", payload)));
            var saveBody = await save.Content.ReadAsStringAsync();
            Assert.True(save.StatusCode == HttpStatusCode.OK && saveBody.Contains("true"), $"SaveNote → {save.StatusCode} {saveBody}");

            var page = await owner.GetAsync($"/Admin/RemedialTrackReports/Student?enrollmentId={a.EnrollmentId}");
            var html = await page.Content.ReadAsStringAsync();
            Assert.True(page.StatusCode == HttpStatusCode.OK, Short(html));
            Assert.DoesNotContain(payload, html);                                         // لا يُعرض خامًا
            Assert.Contains("&lt;script&gt;", html);                                      // يُعرض مُرمَّزًا
        }

        // ───────────── نطاق الدفعات على SQL Server الحقيقي (CompatibilityLevel 120، بلا OPENJSON) ─────────────

        [SqlServerFact]
        public async Task BatchScope_OnRealSqlServer_FiltersPublicationsAndDashboard_WithoutOpenJson()
        {
            using var scope = _factory.Services.CreateScope();
            var publications = scope.ServiceProvider.GetRequiredService<QdratNew.Services.RemedialTracks.IRemedialTrackPublicationService>();
            var reports = scope.ServiceProvider.GetRequiredService<QdratNew.Services.RemedialTracks.IRemedialTrackReportService>();

            var allowed = new QdratNew.ViewModels.RemedialTracks.RemedialTrackBatchScope(new HashSet<int> { _seed.BatchId, -1, -2 });
            var foreign = new QdratNew.ViewModels.RemedialTracks.RemedialTrackBatchScope(new HashSet<int> { -1, -2 });

            var mine = await publications.GetIndexAsync(1, allowed);
            Assert.Contains(mine.Items, i => i.Id == _seed.PublicationId);
            var theirs = await publications.GetIndexAsync(1, foreign);
            Assert.DoesNotContain(theirs.Items, i => i.Id == _seed.PublicationId);

            var filter = new QdratNew.ViewModels.RemedialTracks.RemedialTrackDashboardFilter { Q = "RTKS7", Status = RemedialTrackEnrollmentStatus.InProgress };
            var dash = await reports.GetDashboardAsync(_seed.PublicationId, filter, allowed);
            Assert.NotNull(dash);
            Assert.Null(await reports.GetDashboardAsync(_seed.PublicationId, filter, foreign));          // دفعة خارج النطاق ← لا بيانات
            Assert.Null(await reports.GetParentReportAsync(_seed.Students[A].EnrollmentId, foreign));
            Assert.Null(await reports.GetBatchReportAsync(_seed.PublicationId, foreign));
            Assert.NotNull(await reports.GetParentReportAsync(_seed.Students[A].EnrollmentId, allowed));
        }

        // ───────────── مفتاح التعطيل RemedialTrack.Enabled ─────────────

        [SqlServerFact]
        public async Task KillSwitch_Disabled_Hides404_ForStudent_KeepsAdminAndData()
        {
            var s = _seed.Students[Kill];
            var c = StudentClient(Kill);

            async Task SetFlagAsync(string value)
            {
                await using var db = RtkRealDb.CreateDb();
                var updated = await db.SystemSettings.Where(x => x.Key == "RemedialTrack.Enabled")
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.Value, value));
                if (updated == 0)
                    db.SystemSettings.Add(new QdratNew.Entities.SystemSetting("RemedialTrack.Enabled", value, "rtks7"));
                await db.SaveChangesAsync();
                using var scope = _factory.Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<IMemoryCache>().Remove("rtk-feature-enabled");
            }

            try
            {
                // مفعّل (الافتراضي) ← يعمل
                await SetFlagAsync("true");
                Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(Base)).StatusCode);

                // معطّل ← 404 برسالة صيانة للصفحات، و404 JSON للنداءات، والأدمن يعمل، والبيانات سليمة
                await SetFlagAsync("false");
                var page = await c.GetAsync(Base);
                Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
                Assert.Contains("للصيانة", Decoded(await page.Content.ReadAsStringAsync()));
                Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Base}/Open?enrollmentId={s.EnrollmentId}")).StatusCode);

                var ping = await c.PostAsync($"{Base}/VideoPing", Json(new
                    { EnrollmentId = s.EnrollmentId, VideoProgressId = s.FirstAxisVideoProgressIds[0], State = "playing", Position = 1, Duration = 100 }));
                Assert.Equal(HttpStatusCode.NotFound, ping.StatusCode);
                Assert.Contains("disabled", await ping.Content.ReadAsStringAsync());

                var owner = Client("rtks7-owner", "Owner");
                Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/Admin/RemedialTrackPublications")).StatusCode);

                await using (var db = RtkRealDb.CreateDb())
                    Assert.True(await db.RemedialTrackEnrollments.AnyAsync(e => e.Id == s.EnrollmentId));   // لا مساس بالبيانات

                // إعادة التفعيل ← يعود فورًا
                await SetFlagAsync("true");
                Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(Base)).StatusCode);
            }
            finally
            {
                await SetFlagAsync("true");
            }
        }
    }
}
