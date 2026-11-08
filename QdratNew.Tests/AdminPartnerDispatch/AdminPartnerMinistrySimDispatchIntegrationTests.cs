using System.Net;
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
    // APD-S5 — عبر HTTP حقيقي: الجمهور في شاشتي إسناد معمل القياس، إخفاء ما تملكه المدرسة عن الأدمن،
    // صفحات المدرسة وعزلها (لا تسرب لمدرسة أخرى)، العقد، وCSRF.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class AdminPartnerMinistrySimDispatchIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string OwnerUser = "apds1-owner";
        private const string NoSendUser = "apds1-no-send";
        private const string PartnerAUser = "apds1-partner-a";
        private const string PartnerBUser = "apds1-partner-b";

        private readonly CustomWebApplicationFactory _factory;
        private ApdSeed _seed = null!;
        private ApdMinistrySimSeed _sim = null!;

        public AdminPartnerMinistrySimDispatchIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        private string AssignBatchUrl => $"/Admin/MinistrySimExam/AssignToBatch/{_sim.ExamId}";
        private string AssignStudentUrl => $"/Admin/MinistrySimExam/AssignToStudent/{_sim.ExamId}";

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ApdTestData.CleanupAsync(db);
            _seed = await ApdTestData.SeedAsync(db);
            _sim = await ApdTestData.PrepareMinistrySimAsync(db, _seed);

            db.Users.AddRange(ApdTestData.NewUser(OwnerUser), ApdTestData.NewUser(PartnerAUser), ApdTestData.NewUser(PartnerBUser));
            db.UserPartners.AddRange(
                new UserPartner { UserId = PartnerAUser, PartnerId = _seed.PartnerA },
                new UserPartner { UserId = PartnerBUser, PartnerId = _seed.PartnerB });
            await db.SaveChangesAsync();

            // مستخدم أدمن له بروفايل بلا وحدة PartnerDispatch
            db.Users.Add(ApdTestData.NewUser(NoSendUser));
            var profile = new AdminProfile { Name = $"{ApdTestData.Marker} {NoSendUser}" };
            db.AdminProfiles.Add(profile);
            await db.SaveChangesAsync();

            db.AdminProfileControllerPermissions.Add(new AdminProfileControllerPermission
            {
                AdminProfileId = profile.Id,
                ControllerName = "PerformanceIndicatorExams",
                AccessLevel = AdminControllerAccessLevel.Read
            });
            db.AdminUserProfiles.Add(new AdminUserProfile { UserId = NoSendUser, AdminProfileId = profile.Id });
            await db.SaveChangesAsync();
        }

        public async Task DisposeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            await ApdTestData.CleanupAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        // =====================================================
        // أدوات HTTP
        // =====================================================
        private HttpClient Client(string userId, string role)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
            return client;
        }

        private HttpClient Owner() => Client(OwnerUser, "Owner");

        private async Task<HttpResponseMessage> PostAsync(HttpClient client, string userId, string url, IEnumerable<KeyValuePair<string, string>> fields, string? cookies = null)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(userId);
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("Cookie", cookies == null ? cookie : $"{cookies}; {cookie}");
            request.Headers.Add("RequestVerificationToken", token);
            return await client.SendAsync(request);
        }

        private static string CookiesOf(HttpResponseMessage response)
            => response.Headers.TryGetValues("Set-Cookie", out var values)
                ? string.Join("; ", values.Select(v => v.Split(';')[0]))
                : "";

        private async Task<(HttpClient Client, string Cookies)> PartnerAsync(string userId, int partnerId)
        {
            var client = Client(userId, "Partner");
            var selected = await PostAsync(client, userId, "/Partner/PartnerContext/Set", new[] { Field("partnerId", partnerId) });
            return (client, CookiesOf(selected));
        }

        private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string cookies)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Cookie", cookies);
            return await client.SendAsync(request);
        }

        private static KeyValuePair<string, string> Field(string name, object value) => new(name, value.ToString()!);

        private static async Task<string> HtmlAsync(HttpResponseMessage response)
            => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        private async Task<T> InDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private List<KeyValuePair<string, string>> PartnersForm() => new()
        {
            Field("isOnline", "true"),
            Field("Audience", (int)DispatchAudience.Partners),
            Field($"PartnerBatchIds[{_seed.PartnerA}]", _seed.BatchA1),
            Field($"PartnerBatchIds[{_seed.PartnerB}]", _seed.BatchB1)
        };

        // A وB عبر الشاشة، والمعهد عبر الخدمة، ومحاولة مكتملة لكل طالب
        private async Task SendAndAttemptAsync()
        {
            var sent = await PostAsync(Owner(), OwnerUser, AssignBatchUrl, PartnersForm());
            Assert.Contains("/PartnerDispatch/Result", sent.Headers.Location!.ToString());

            var platform = await PostAsync(Owner(), OwnerUser, AssignBatchUrl, new[] { Field("isOnline", "true"), Field("batchIds", _seed.BatchI1) });
            Assert.Contains($"/Details/{_sim.ExamId}", platform.Headers.Location!.ToString());

            await InDbAsync(async db =>
            {
                await ApdTestData.CompleteSimAttemptAsync(db, _sim, _seed.ActiveStudentA, 70);
                await ApdTestData.CompleteSimAttemptAsync(db, _sim, _sim.StudentB, 30);
                return await ApdTestData.CompleteSimAttemptAsync(db, _sim, _seed.StudentI1, 90);
            });
        }

        // =====================================================
        // شاشتا الإسناد في الأدمن
        // =====================================================
        [Fact]
        public async Task AssignToBatchPage_ShowsPicker_PlatformBatchesOnly_AndMarksAssigned()
        {
            await PostAsync(Owner(), OwnerUser, AssignBatchUrl, new[] { Field("isOnline", "true"), Field("PlatformBatchIds", _seed.BatchI1) });

            var html = await HtmlAsync(await Owner().GetAsync(AssignBatchUrl));

            Assert.Contains("data-apd-picker", html);
            Assert.Contains($"apd-platform-{_seed.BatchI1}", html);
            Assert.DoesNotContain($"apd-platform-{_seed.BatchA1}", html);
            Assert.Contains("data-apd-already-assigned", html);
            Assert.Contains("المدارس الشريكة</strong>", html);
        }

        [Fact]
        public async Task WithoutSendPermission_PartnersHidden_AndForbidden_NothingWritten()
        {
            var noSend = Client(NoSendUser, "Admin");

            var html = await HtmlAsync(await noSend.GetAsync(AssignBatchUrl));
            Assert.Contains("data-apd-picker", html);
            Assert.DoesNotContain("المدارس الشريكة</strong>", html);

            var studentsPage = await HtmlAsync(await noSend.GetAsync(AssignStudentUrl));
            Assert.DoesNotContain("مدرسة شريكة:", studentsPage);

            var response = await PostAsync(noSend, NoSendUser, AssignBatchUrl, PartnersForm());
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            var picker = await noSend.GetAsync($"/Admin/MinistrySimExam/GetStudentsForBatch?examId={_sim.ExamId}&batchId={_seed.BatchA1}");
            Assert.Equal(HttpStatusCode.Forbidden, picker.StatusCode);

            Assert.False(await InDbAsync(db => db.MinistrySimExamAssignmentsToBatches.AnyAsync(a => a.MinistrySimExamId == _sim.ExamId)));
        }

        [Fact]
        public async Task Platform_LegacyBatchIds_SchoolBatch_IsRefused_NothingWritten()
        {
            var response = await PostAsync(Owner(), OwnerUser, AssignBatchUrl, new[] { Field("isOnline", "true"), Field("batchIds", _seed.BatchA1) });

            Assert.Contains($"AssignToBatch/{_sim.ExamId}", response.Headers.Location!.ToString());
            var html = await HtmlAsync(await GetAsync(Owner(), AssignBatchUrl, CookiesOf(response)));
            Assert.Contains("ليست من دفعات المنصة", html);
            Assert.False(await InDbAsync(db => db.MinistrySimExamAssignmentsToBatches.AnyAsync(a => a.MinistrySimExamId == _sim.ExamId)));
        }

        [Fact]
        public async Task Partners_Flow_OwnedBySchools_ResultPage()
        {
            var sent = await PostAsync(Owner(), OwnerUser, AssignBatchUrl, PartnersForm());
            Assert.Contains("/PartnerDispatch/Result", sent.Headers.Location!.ToString());

            var result = await HtmlAsync(await GetAsync(Owner(), "/Admin/PartnerDispatch/Result", CookiesOf(sent)));
            Assert.Contains("معمل القياس", result);
            Assert.Contains("MinistrySimExamAssignments", result);

            var owners = await InDbAsync(db => db.MinistrySimExamAssignmentsToBatches.AsNoTracking()
                .Where(a => a.MinistrySimExamId == _sim.ExamId)
                .Select(a => new { a.BatchId, a.OwnerPartnerId })
                .ToListAsync());
            Assert.Contains(owners, o => o.BatchId == _seed.BatchA1 && o.OwnerPartnerId == _seed.PartnerA);
            Assert.Contains(owners, o => o.BatchId == _seed.BatchB1 && o.OwnerPartnerId == _seed.PartnerB);
        }

        [Fact]
        public async Task Partners_ContractWithoutFeature_NoOverride_IsRefused()
        {
            await InDbAsync(async db =>
            {
                var contract = await db.PartnerSubscriptions.FirstAsync(s => s.PartnerId == _seed.PartnerA);
                contract.CanUseMinistrySimExams = false;
                return await db.SaveChangesAsync();
            });

            var response = await PostAsync(Owner(), OwnerUser, AssignBatchUrl, PartnersForm());
            var html = await HtmlAsync(await GetAsync(Owner(), AssignBatchUrl, CookiesOf(response)));

            Assert.Contains("العقد لا يشمل معمل القياس", html);
            Assert.False(await InDbAsync(db => db.MinistrySimExamAssignmentsToBatches.AnyAsync(a => a.MinistrySimExamId == _sim.ExamId)));
        }

        [Fact]
        public async Task AssignToStudent_SchoolBatch_StudentsOfSchoolOnly_OwnedBySchool()
        {
            var studentsPage = await HtmlAsync(await Owner().GetAsync(AssignStudentUrl));
            Assert.Contains("مدرسة شريكة:", studentsPage);

            var picker = await HtmlAsync(await Owner().GetAsync($"/Admin/MinistrySimExam/GetStudentsForBatch?examId={_sim.ExamId}&batchId={_seed.BatchA1}"));
            Assert.Contains($"student-{_seed.ActiveStudentA}", picker);
            var foreign = await InDbAsync(db => db.StudentBatchEnrollments.AsNoTracking()
                .Where(e => e.BatchId == _seed.BatchA1 && e.Student.Branch.PartnerId == _seed.PartnerB)
                .Select(e => e.StudentID)
                .SingleAsync());
            Assert.DoesNotContain($"student-{foreign}\"", picker);

            var sent = await PostAsync(Owner(), OwnerUser, AssignStudentUrl, new[]
            {
                Field("isOnline", "true"),
                Field("batchId", _seed.BatchA1),
                Field("studentIds", _seed.ActiveStudentA)
            });
            Assert.Contains("/PartnerDispatch/Result", sent.Headers.Location!.ToString());

            var row = await InDbAsync(db => db.MinistrySimExamAssignmentsToStudents.AsNoTracking().SingleAsync(a => a.MinistrySimExamId == _sim.ExamId));
            Assert.Equal(_seed.ActiveStudentA, row.StudentId);
            Assert.Equal(_seed.PartnerA, row.OwnerPartnerId);
        }

        // =====================================================
        // الأدمن لا يرى ما تملكه المدارس
        // =====================================================
        [Fact]
        public async Task Admin_SeesInstituteResultsOnly()
        {
            await SendAndAttemptAsync();
            var owner = Owner();

            var select = await HtmlAsync(await owner.GetAsync($"/Admin/MinistrySimExam/SelectStudentForResult/{_sim.ExamId}"));
            Assert.Contains($"studentId={_seed.StudentI1}\"", select);
            Assert.DoesNotContain($"studentId={_seed.ActiveStudentA}\"", select);
            Assert.DoesNotContain($"studentId={_sim.StudentB}\"", select);

            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/Admin/MinistrySimExam/StudentResult/{_sim.ExamId}?studentId={_seed.StudentI1}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Admin/MinistrySimExam/StudentResult/{_sim.ExamId}?studentId={_seed.ActiveStudentA}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Admin/MinistrySimExam/StudentQuestionReview/{_sim.ExamId}?studentId={_seed.ActiveStudentA}&stageNumber=1")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Admin/MinistrySimExam/ParentReport?examId={_sim.ExamId}&studentId={_sim.StudentB}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Admin/MinistrySimExam/ParentReportPdf?examId={_sim.ExamId}&studentId={_sim.StudentB}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Admin/MinistrySimExam/BatchAnalytics?examId={_sim.ExamId}&batchId={_seed.BatchA1}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/Admin/MinistrySimExam/BatchAnalytics?examId={_sim.ExamId}&batchId={_seed.BatchI1}")).StatusCode);

            var details = await HtmlAsync(await owner.GetAsync($"/Admin/MinistrySimExam/Details/{_sim.ExamId}"));
            Assert.Contains("مُسنَد أيضًا لـ 2 مدرسة شريكة", details);

            var overview = await HtmlAsync(await owner.GetAsync("/Admin/MinistrySimExamAssignments"));
            Assert.DoesNotContain($"{ApdTestData.Marker} دفعة A1", overview);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Admin/MinistrySimExamAssignments/BatchDetails?batchId={_seed.BatchA1}")).StatusCode);

            var attemptA = await InDbAsync(db => db.MinistrySimExamStudentAttempts.Where(a => a.MinistrySimExamId == _sim.ExamId && a.StudentId == _seed.ActiveStudentA).Select(a => a.Id).SingleAsync());
            var note = await PostAsync(owner, OwnerUser, "/Admin/MinistrySimExam/SaveParentReportNote", new[] { Field("attemptId", attemptA), Field("note", "من الأدمن") });
            Assert.Equal(HttpStatusCode.NotFound, note.StatusCode);
        }

        // =====================================================
        // منطقة المدرسة
        // =====================================================
        [Fact]
        public async Task Partner_SeesOwnResults_AndNothingOfOtherSchool()
        {
            await SendAndAttemptAsync();
            var (a, cookiesA) = await PartnerAsync(PartnerAUser, _seed.PartnerA);

            var index = await GetAsync(a, "/Partner/MinistrySimExams", cookiesA);
            var indexHtml = await HtmlAsync(index);
            Assert.Equal(HttpStatusCode.OK, index.StatusCode);
            Assert.Contains($"data-mse-exam=\"{_sim.ExamId}\"", indexHtml);
            Assert.Contains("من المعهد", indexHtml);

            var students = await HtmlAsync(await GetAsync(a, $"/Partner/MinistrySimExams/BatchStudents/{_sim.ExamId}", cookiesA));
            Assert.Contains($"data-mse-student=\"{_seed.ActiveStudentA}\"", students);
            Assert.DoesNotContain($"data-mse-student=\"{_sim.StudentB}\"", students);
            Assert.DoesNotContain($"data-mse-student=\"{_seed.StudentI1}\"", students);

            var result = await GetAsync(a, $"/Partner/MinistrySimExams/StudentResult/{_sim.ExamId}?studentId={_seed.ActiveStudentA}", cookiesA);
            var resultHtml = await HtmlAsync(result);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.DoesNotContain("/Admin/MinistrySimExam", resultHtml);
            Assert.DoesNotContain("MinistrySimExam/ParentReportPdf", resultHtml);
            Assert.Contains("/Partner/MinistrySimExams/ParentReport", resultHtml);

            Assert.Equal(HttpStatusCode.OK, (await GetAsync(a, $"/Partner/MinistrySimExams/StudentQuestionReview/{_sim.ExamId}?studentId={_seed.ActiveStudentA}&stageNumber=1", cookiesA)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await GetAsync(a, $"/Partner/MinistrySimExams/BatchAnalytics?examId={_sim.ExamId}&batchId={_seed.BatchA1}", cookiesA)).StatusCode);

            var report = await GetAsync(a, $"/Partner/MinistrySimExams/ParentReport?examId={_sim.ExamId}&studentId={_seed.ActiveStudentA}", cookiesA);
            var reportHtml = await HtmlAsync(report);
            Assert.Equal(HttpStatusCode.OK, report.StatusCode);
            Assert.Contains($"{ApdTestData.Marker} مدرسة A", reportHtml);
            Assert.Contains("بالتعاون مع معهد القدرات", reportHtml);
            Assert.Contains("data-mse-print", reportHtml);
            Assert.DoesNotContain("MinistrySimExam/ParentReportPdf", reportHtml);

            // لا شيء لمدرسة أخرى أو للمعهد
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(a, $"/Partner/MinistrySimExams/StudentResult/{_sim.ExamId}?studentId={_sim.StudentB}", cookiesA)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(a, $"/Partner/MinistrySimExams/StudentResult/{_sim.ExamId}?studentId={_seed.StudentI1}", cookiesA)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(a, $"/Partner/MinistrySimExams/ParentReport?examId={_sim.ExamId}&studentId={_sim.StudentB}", cookiesA)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(a, $"/Partner/MinistrySimExams/BatchAnalytics?examId={_sim.ExamId}&batchId={_seed.BatchB1}", cookiesA)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(a, $"/Partner/MinistrySimExams/BatchStudents/{_sim.ExamId}?batchId={_seed.BatchB1}", cookiesA)).StatusCode);

            var (b, cookiesB) = await PartnerAsync(PartnerBUser, _seed.PartnerB);
            var studentsB = await HtmlAsync(await GetAsync(b, $"/Partner/MinistrySimExams/BatchStudents/{_sim.ExamId}", cookiesB));
            Assert.Contains($"data-mse-student=\"{_sim.StudentB}\"", studentsB);
            Assert.DoesNotContain($"data-mse-student=\"{_seed.ActiveStudentA}\"", studentsB);
        }

        [Fact]
        public async Task Partner_Note_Hide_RequireCsrf_AndOwnAttempt()
        {
            await SendAndAttemptAsync();
            var (a, cookiesA) = await PartnerAsync(PartnerAUser, _seed.PartnerA);

            var attempts = await InDbAsync(db => db.MinistrySimExamStudentAttempts.AsNoTracking()
                .Where(x => x.MinistrySimExamId == _sim.ExamId)
                .ToDictionaryAsync(x => x.StudentId, x => x.Id));

            var own = await PostAsync(a, PartnerAUser, "/Partner/MinistrySimExams/SaveParentReportNote", new[] { Field("attemptId", attempts[_seed.ActiveStudentA]), Field("note", "توصية") }, cookiesA);
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);

            var other = await PostAsync(a, PartnerAUser, "/Partner/MinistrySimExams/SaveParentReportNote", new[] { Field("attemptId", attempts[_sim.StudentB]), Field("note", "تسرب") }, cookiesA);
            Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);

            var noToken = new HttpRequestMessage(HttpMethod.Post, $"/Partner/MinistrySimExams/Hide/{_sim.ExamId}")
            {
                Content = new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>())
            };
            noToken.Headers.Add("Cookie", cookiesA);
            Assert.Equal(HttpStatusCode.BadRequest, (await a.SendAsync(noToken)).StatusCode);

            var hidden = await PostAsync(a, PartnerAUser, $"/Partner/MinistrySimExams/Hide/{_sim.ExamId}", Array.Empty<KeyValuePair<string, string>>(), cookiesA);
            Assert.Equal(HttpStatusCode.Redirect, hidden.StatusCode);
            Assert.False(await InDbAsync(db => db.MinistrySimExamAssignmentsToBatches.AnyAsync(x => x.OwnerPartnerId == _seed.PartnerA && x.HiddenByOwnerAt == null)));
            Assert.True(await InDbAsync(db => db.MinistrySimExamAssignmentsToBatches.AnyAsync(x => x.OwnerPartnerId == _seed.PartnerB && x.HiddenByOwnerAt == null)));

            Assert.Equal("توصية", await InDbAsync(db => db.MinistrySimExamStudentAttempts.Where(x => x.Id == attempts[_seed.ActiveStudentA]).Select(x => x.RecommendationsNote).SingleAsync()));
            Assert.Null(await InDbAsync(db => db.MinistrySimExamStudentAttempts.Where(x => x.Id == attempts[_sim.StudentB]).Select(x => x.RecommendationsNote).SingleAsync()));
        }

        [Fact]
        public async Task Partner_ContractWithoutMinistrySim_IsDenied()
        {
            await InDbAsync(async db =>
            {
                var contract = await db.PartnerSubscriptions.FirstAsync(s => s.PartnerId == _seed.PartnerA);
                contract.CanUseMinistrySimExams = false;
                return await db.SaveChangesAsync();
            });

            var (a, cookiesA) = await PartnerAsync(PartnerAUser, _seed.PartnerA);
            var response = await GetAsync(a, "/Partner/MinistrySimExams", cookiesA);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
