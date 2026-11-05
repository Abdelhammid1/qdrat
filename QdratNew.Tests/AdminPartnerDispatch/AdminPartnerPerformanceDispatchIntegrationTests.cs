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
    // APD-S4 — عبر HTTP حقيقي: الجمهور في شاشة مؤشر الأداء، الإخفاء عن الأدمن، صفحات الشريك وعزلها،
    // القراءة بعد انتهاء العقد، CSRF، والطالب خارج النسخة.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class AdminPartnerPerformanceDispatchIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string CreateUrl = "/Admin/PerformanceIndicatorExams/Create";
        private const string OwnerUser = "apds1-owner";
        private const string NoSendUser = "apds1-no-send";
        private const string PartnerAUser = "apds1-partner-a";
        private const string PartnerBUser = "apds1-partner-b";

        private readonly CustomWebApplicationFactory _factory;
        private ApdSeed _seed = null!;
        private ApdPerformanceSeed _kpi = null!;

        public AdminPartnerPerformanceDispatchIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ApdTestData.CleanupAsync(db);
            _seed = await ApdTestData.SeedAsync(db);
            _kpi = await ApdTestData.PreparePerformanceAsync(db, _seed);

            db.Users.AddRange(ApdTestData.NewUser(OwnerUser), ApdTestData.NewUser(PartnerAUser), ApdTestData.NewUser(PartnerBUser));
            db.UserPartners.AddRange(
                new UserPartner { UserId = PartnerAUser, PartnerId = _seed.PartnerA },
                new UserPartner { UserId = PartnerBUser, PartnerId = _seed.PartnerB });
            await db.SaveChangesAsync();

            db.Users.Add(ApdTestData.NewUser(NoSendUser));
            var profile = new AdminProfile { Name = $"{ApdTestData.Marker} {NoSendUser}" };
            db.AdminProfiles.Add(profile);
            await db.SaveChangesAsync();

            var row = new AdminProfileControllerPermission
            {
                AdminProfileId = profile.Id,
                ControllerName = "PerformanceIndicatorExams",
                AccessLevel = AdminControllerAccessLevel.Read,
                HasCustomPermissions = true
            };
            foreach (var action in new[] { "Add", "ConfirmSend", "Read" })
                row.CustomPermissions.Add(new AdminProfileCustomPermission { ActionName = action, IsAllowed = true });
            db.AdminProfileControllerPermissions.Add(row);
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

        private List<KeyValuePair<string, string>> BaseForm(string title) => new()
        {
            Field("ExamTitle", title),
            Field("CurriculumId", _kpi.CurriculumId),
            Field("IsOnline", "true"),
            Field("TotalQuestions", 2),
            Field("ManualSelection", "false"),
            Field("StartAt", DateTime.Today.ToString("yyyy-MM-ddTHH:mm")),
            Field("EndAt", DateTime.Today.AddDays(7).ToString("yyyy-MM-ddTHH:mm"))
        };

        private List<KeyValuePair<string, string>> PartnersForm(string title)
        {
            var form = BaseForm(title);
            form.Add(Field("Audience", (int)DispatchAudience.Partners));
            form.Add(Field($"PartnerBatchIds[{_seed.PartnerA}]", _seed.BatchA1));
            return form;
        }

        private async Task<T> InDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        // يُرسل القالب الجاهز للمدرسة A عبر الخدمة، ويُرجع رقم نسخة A
        private async Task<int> SendCopyToAAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<IPartnerPerformanceDispatchService>().DispatchAsync(
                _kpi.TemplateId,
                new[] { new PartnerDispatchTarget { PartnerId = _seed.PartnerA, PartnerName = "A", BatchIds = new List<int> { _seed.BatchA1 } } },
                new DispatchActor(ApdTestData.SenderId, "مرسل"));
            Assert.True(outcome.Results.Single().Succeeded, outcome.Results.Single().Message);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.PerformanceIndicatorExams.AsNoTracking()
                .Where(e => e.OwnerPartnerId == _seed.PartnerA)
                .Select(e => e.Id)
                .SingleAsync();
        }

        // =====================================================
        // شاشة الإنشاء
        // =====================================================
        [Fact]
        public async Task CreatePage_ShowsPicker_PlatformBatchesOnly()
        {
            var html = await HtmlAsync(await Owner().GetAsync(CreateUrl));

            Assert.Contains("data-apd-picker", html);
            Assert.Contains($"apd-platform-{_seed.BatchI1}", html);
            Assert.DoesNotContain($"apd-platform-{_seed.BatchA1}", html);
            Assert.DoesNotContain("name=\"SelectedBatchIds\"", html);
        }

        [Fact]
        public async Task CreatePage_WithoutSendPermission_HidesPartnersOption()
        {
            var html = await HtmlAsync(await Client(NoSendUser, "Admin").GetAsync(CreateUrl));

            Assert.Contains("data-apd-picker", html);
            Assert.DoesNotContain("المدارس الشريكة</strong>", html);
        }

        [Fact]
        public async Task Partners_WithoutSendPermission_IsForbidden_NothingWritten()
        {
            var title = $"{ApdTestData.Marker} بلا صلاحية";
            var response = await PostAsync(Client(NoSendUser, "Admin"), NoSendUser, CreateUrl, PartnersForm(title));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(await InDbAsync(db => db.PerformanceIndicatorExams.AnyAsync(e => e.Title == title)));
        }

        [Fact]
        public async Task Platform_PartnerBatch_IsRefused_NothingWritten()
        {
            var title = $"{ApdTestData.Marker} منصة بدفعة شريك";
            var form = BaseForm(title);
            form.Add(Field("Audience", (int)DispatchAudience.Platform));
            form.Add(Field("PlatformBatchIds", _seed.BatchA1));

            var html = await HtmlAsync(await PostAsync(Owner(), OwnerUser, CreateUrl, form));

            Assert.Contains("ليست من دفعات المنصة", html);
            Assert.False(await InDbAsync(db => db.PerformanceIndicatorExams.AnyAsync(e => e.Title == title)));
        }

        [Fact]
        public async Task Platform_LegacySelectedBatchIds_PartnerBatch_IsRefused()
        {
            var title = $"{ApdTestData.Marker} حقل قديم";
            var form = BaseForm(title);
            form.Add(Field("SelectedBatchIds", _seed.BatchA1));

            var html = await HtmlAsync(await PostAsync(Owner(), OwnerUser, CreateUrl, form));

            Assert.Contains("ليست من دفعات المنصة", html);
            Assert.False(await InDbAsync(db => db.PerformanceIndicatorExams.AnyAsync(e => e.Title == title)));
        }

        [Fact]
        public async Task Platform_PartnerStudent_IsRefused()
        {
            var title = $"{ApdTestData.Marker} طالب شريك";
            var form = BaseForm(title);
            form.Add(Field("PlatformBatchIds", _seed.BatchI1));
            form.Add(Field("SelectedStudentIds", _seed.ActiveStudentA));

            var html = await HtmlAsync(await PostAsync(Owner(), OwnerUser, CreateUrl, form));

            Assert.Contains("يتبع مدرسة شريكة", html);
            Assert.False(await InDbAsync(db => db.PerformanceIndicatorExams.AnyAsync(e => e.Title == title)));
        }

        [Fact]
        public async Task Platform_InstituteBatch_BehavesAsBefore_GoesToConfirmSend()
        {
            var title = $"{ApdTestData.Marker} منصة";
            var form = BaseForm(title);
            form.Add(Field("PlatformBatchIds", _seed.BatchI1));

            var response = await PostAsync(Owner(), OwnerUser, CreateUrl, form);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("ConfirmSend", response.Headers.Location!.ToString());

            var exam = await InDbAsync(db => db.PerformanceIndicatorExams.AsNoTracking().SingleAsync(e => e.Title == title));
            Assert.Null(exam.OwnerPartnerId);
            Assert.False(exam.IsPartnerTemplate);
            Assert.False(exam.IsArchived);
            Assert.False(exam.IsSent);
            Assert.True(await InDbAsync(db => db.PerformanceIndicatorExamToBatch.AnyAsync(l => l.PerformanceIndicatorExamId == exam.Id && l.BatchId == _seed.BatchI1)));
            Assert.True(exam.TotalQuestions > 0);
            Assert.Equal(exam.TotalQuestions, await InDbAsync(db => db.PerformanceIndicatorExamQuestions.CountAsync(q => q.PerformanceIndicatorExamId == exam.Id)));
        }

        [Fact]
        public async Task Partners_ContractWithoutFeature_NoOverride_IsRefused()
        {
            await InDbAsync(async db =>
            {
                var contract = await db.PartnerSubscriptions.FirstAsync(s => s.PartnerId == _seed.PartnerA);
                contract.CanUsePerformanceIndicatorExams = false;
                return await db.SaveChangesAsync();
            });

            var title = $"{ApdTestData.Marker} عقد بلا مؤشر";
            var html = await HtmlAsync(await PostAsync(Owner(), OwnerUser, CreateUrl, PartnersForm(title)));

            Assert.Contains("العقد لا يشمل مؤشر الأداء", html);
            Assert.False(await InDbAsync(db => db.PerformanceIndicatorExams.AnyAsync(e => e.Title == title)));
        }

        [Fact]
        public async Task Partners_Flow_Create_ConfirmSend_Result_CopyOwnedBySchool()
        {
            var title = $"{ApdTestData.Marker} للمدارس";

            var created = await PostAsync(Owner(), OwnerUser, CreateUrl, PartnersForm(title));
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            Assert.Contains("ConfirmSend", created.Headers.Location!.ToString());
            var cookies = CookiesOf(created);

            var template = await InDbAsync(db => db.PerformanceIndicatorExams.AsNoTracking().SingleAsync(e => e.Title == title));
            Assert.True(template.IsPartnerTemplate);
            Assert.True(template.IsArchived);
            Assert.Null(template.OwnerPartnerId);
            Assert.False(await InDbAsync(db => db.PerformanceIndicatorExamStudents.AnyAsync(s => s.PerformanceIndicatorExamId == template.Id)));

            var confirmPage = await GetAsync(Owner(), $"/Admin/PerformanceIndicatorExams/ConfirmSend/{template.Id}", cookies);
            var confirmHtml = await HtmlAsync(confirmPage);
            Assert.Equal(HttpStatusCode.OK, confirmPage.StatusCode);
            Assert.Contains($"apd-partner-{_seed.PartnerA}", confirmHtml);
            Assert.DoesNotContain("دفعات المعهد فقط", confirmHtml);

            // زر الإرسال في صفحة المراجعة بلا اختيار: يعود لشاشة الاختيار ولا يرسل
            var noSelection = await PostAsync(Owner(), OwnerUser, "/Admin/PerformanceIndicatorExams/ConfirmSendPost", new[] { Field("examId", template.Id) });
            Assert.Contains("ConfirmSend", noSelection.Headers.Location!.ToString());
            Assert.False(await InDbAsync(db => db.PerformanceIndicatorExams.AnyAsync(e => e.OwnerPartnerId == _seed.PartnerA)));

            var sent = await PostAsync(Owner(), OwnerUser, "/Admin/PerformanceIndicatorExams/ConfirmSendPost", new[]
            {
                Field("examId", template.Id),
                Field("Audience", (int)DispatchAudience.Partners),
                Field($"PartnerBatchIds[{_seed.PartnerA}]", _seed.BatchA1)
            });
            Assert.Contains("/PartnerDispatch/Result", sent.Headers.Location!.ToString());

            var result = await HtmlAsync(await GetAsync(Owner(), "/Admin/PartnerDispatch/Result", CookiesOf(sent)));
            Assert.Contains("اختبار مؤشر الأداء", result);
            Assert.Contains("PerformanceIndicatorExams/Create", result);

            var copy = await InDbAsync(db => db.PerformanceIndicatorExams.AsNoTracking().SingleAsync(e => e.OwnerPartnerId == _seed.PartnerA && e.Title == title));
            Assert.NotEqual(template.ReferenceCode, copy.ReferenceCode);
            Assert.Equal(new[] { _seed.ActiveStudentA }, await InDbAsync(db =>
                db.PerformanceIndicatorExamStudents.Where(s => s.PerformanceIndicatorExamId == copy.Id).Select(s => s.StudentId).ToListAsync()));

            // القالب في أرشيف الأدمن بوسمه، والنسخة لا تظهر
            var archive = await HtmlAsync(await Owner().GetAsync("/Admin/PerformanceIndicatorExams?showArchived=true"));
            Assert.Contains("قالب إرسال للشركاء", archive);
            Assert.Contains($"ConfirmSend/{template.Id}", archive);
            Assert.DoesNotContain($"Details/{copy.Id}", archive);
        }

        [Fact]
        public async Task ConfirmSend_Template_WithoutSendPermission_IsForbidden()
        {
            var response = await Client(NoSendUser, "Admin").GetAsync($"/Admin/PerformanceIndicatorExams/ConfirmSend/{_kpi.TemplateId}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Restore_DoesNotTakeTemplateOutOfArchive()
        {
            await PostAsync(Owner(), OwnerUser, "/Admin/PerformanceIndicatorExams/RestorePerformanceIndicatorExams", new[] { Field("selectedExamIds", _kpi.TemplateId) });

            Assert.True(await InDbAsync(db => db.PerformanceIndicatorExams.AsNoTracking().Where(e => e.Id == _kpi.TemplateId).Select(e => e.IsArchived).SingleAsync()));
        }

        // =====================================================
        // الإخفاء عن الأدمن
        // =====================================================
        [Fact]
        public async Task SchoolCopy_IsHiddenFromEveryAdminPage()
        {
            var id = await SendCopyToAAsync();
            var owner = Owner();

            foreach (var url in new[]
            {
                $"/Admin/PerformanceIndicatorExams/Details/{id}",
                $"/Admin/PerformanceIndicatorExams/Review/{id}",
                $"/Admin/PerformanceIndicatorExams/ConfirmSend/{id}",
                $"/Admin/PerformanceIndicatorExams/ExamStudents/{id}",
                $"/Admin/PerformanceIndicatorExams/StudentExamQuestions?examId={id}&studentId={_seed.ActiveStudentA}",
                $"/Admin/PerformanceIndicatorExams/ManageArchiveAccess?examId={id}",
                $"/Admin/PerformanceExamReports/StudentExamReport?studentId={_seed.ActiveStudentA}&examId={id}",
                $"/Admin/PerformanceAnalysis/BatchReport?batchId={_seed.BatchA1}&examId={id}"
            })
            {
                var response = await owner.GetAsync(url);
                Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Redirect, $"{url}: {response.StatusCode}");
            }

            // لوحة الأداء ترد برسالة «لم يتم العثور» داخل الصفحة
            var dashboardReport = await HtmlAsync(await owner.GetAsync($"/Admin/PerformanceDashboard/ExamReport?examId={id}"));
            Assert.DoesNotContain(ApdTestData.Marker, dashboardReport);

            var list = await HtmlAsync(await owner.GetAsync("/Admin/PerformanceIndicatorExams"));
            Assert.DoesNotContain($"Details/{id}", list);
        }

        [Fact]
        public async Task SchoolCopy_AdminWritePaths_DoNothing()
        {
            var id = await SendCopyToAAsync();

            await PostAsync(Owner(), OwnerUser, $"/Admin/PerformanceIndicatorExams/Delete/{id}", Array.Empty<KeyValuePair<string, string>>());
            await PostAsync(Owner(), OwnerUser, "/Admin/PerformanceIndicatorExams/ArchivePerformanceIndicatorExams", new[] { Field("selectedExamIds", id) });
            await PostAsync(Owner(), OwnerUser, "/Admin/PerformanceIndicatorExams/ConfirmSendPost", new[] { Field("examId", id) });
            var before = await InDbAsync(db => db.PerformanceIndicatorExamQuestions.AsNoTracking()
                .Where(q => q.PerformanceIndicatorExamId == id).Select(q => q.QuestionId).OrderBy(q => q).ToListAsync());
            await PostAsync(Owner(), OwnerUser, "/Admin/PerformanceIndicatorExams/ReplaceQuestionPage", new[]
            {
                Field("ExamId", id), Field("OldQuestionId", _kpi.QuestionA), Field("NewQuestionId", _seed.QuestionId), Field("SectionId", _kpi.SectionId)
            });

            var exam = await InDbAsync(db => db.PerformanceIndicatorExams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id));
            Assert.NotNull(exam);
            Assert.False(exam!.IsArchived);
            Assert.Equal(before, await InDbAsync(db => db.PerformanceIndicatorExamQuestions.AsNoTracking()
                .Where(q => q.PerformanceIndicatorExamId == id).Select(q => q.QuestionId).OrderBy(q => q).ToListAsync()));
            Assert.Equal(1, await InDbAsync(db => db.PerformanceIndicatorExamStudents.AsNoTracking().CountAsync(s => s.PerformanceIndicatorExamId == id)));
        }

        // =====================================================
        // صفحات الشريك
        // =====================================================
        [Fact]
        public async Task PartnerPages_OwnerSeesThem_OtherSchoolGets404()
        {
            var id = await SendCopyToAAsync();
            await InDbAsync(db => db.PerformanceIndicatorExamStudents
                .Where(s => s.PerformanceIndicatorExamId == id)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.IsCompleted, true)));

            var (a, cookiesA) = await PartnerAsync(PartnerAUser, _seed.PartnerA);

            var index = await HtmlAsync(await GetAsync(a, "/Partner/PerformanceIndicatorExams", cookiesA));
            Assert.Contains($"data-performance-exam=\"{id}\"", index);
            Assert.Contains("من المعهد", index);
            Assert.DoesNotContain($"data-performance-exam=\"{_kpi.TemplateId}\"", index);

            var pages = new[]
            {
                $"/Partner/PerformanceIndicatorExams/ExamStudents/{id}",
                $"/Partner/PerformanceIndicatorExams/StudentExamQuestions?examId={id}&studentId={_seed.ActiveStudentA}",
                $"/Partner/PerformanceIndicatorExams/BatchReport?examId={id}&batchId={_seed.BatchA1}"
            };

            foreach (var url in pages)
                Assert.Equal(HttpStatusCode.OK, (await GetAsync(a, url, cookiesA)).StatusCode);

            var (b, cookiesB) = await PartnerAsync(PartnerBUser, _seed.PartnerB);

            foreach (var url in pages)
                Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(b, url, cookiesB)).StatusCode);

            var indexB = await HtmlAsync(await GetAsync(b, "/Partner/PerformanceIndicatorExams", cookiesB));
            Assert.DoesNotContain($"data-performance-exam=\"{id}\"", indexB);

            var hide = await PostAsync(b, PartnerBUser, $"/Partner/PerformanceIndicatorExams/Hide/{id}", Array.Empty<KeyValuePair<string, string>>(), cookiesB);
            Assert.Equal(HttpStatusCode.NotFound, hide.StatusCode);
        }

        [Fact]
        public async Task PartnerPages_ContractWithoutFeature_AreForbidden()
        {
            var id = await SendCopyToAAsync();
            await InDbAsync(async db =>
            {
                var contract = await db.PartnerSubscriptions.FirstAsync(s => s.PartnerId == _seed.PartnerA);
                contract.CanUsePerformanceIndicatorExams = false;
                return await db.SaveChangesAsync();
            });

            var (a, cookies) = await PartnerAsync(PartnerAUser, _seed.PartnerA);

            Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(a, "/Partner/PerformanceIndicatorExams", cookies)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(a, $"/Partner/PerformanceIndicatorExams/ExamStudents/{id}", cookies)).StatusCode);
        }

        [Fact]
        public async Task PartnerPages_AfterContractEnds_ReadAllowed_ManageForbidden()
        {
            var id = await SendCopyToAAsync();
            await InDbAsync(async db =>
            {
                var contract = await db.PartnerSubscriptions.FirstAsync(s => s.PartnerId == _seed.PartnerA);
                contract.StartDate = DateTime.Today.AddDays(-60);
                contract.EndDate = DateTime.Today.AddDays(-2);
                contract.AccessUntilDate = DateTime.Today.AddDays(30);
                return await db.SaveChangesAsync();
            });

            var (a, cookies) = await PartnerAsync(PartnerAUser, _seed.PartnerA);

            var index = await GetAsync(a, "/Partner/PerformanceIndicatorExams", cookies);
            Assert.Equal(HttpStatusCode.OK, index.StatusCode);
            var indexHtml = await HtmlAsync(index);
            Assert.Contains($"data-performance-exam=\"{id}\"", indexHtml);
            Assert.DoesNotContain("/Hide/", indexHtml);

            Assert.Equal(HttpStatusCode.OK, (await GetAsync(a, $"/Partner/PerformanceIndicatorExams/ExamStudents/{id}", cookies)).StatusCode);

            // الإدارة تتطلب عقدًا نشطًا: تحويل لصفحة «لا اشتراك نشط» بلا أي تغيير
            var hide = await PostAsync(a, PartnerAUser, $"/Partner/PerformanceIndicatorExams/Hide/{id}", Array.Empty<KeyValuePair<string, string>>(), cookies);
            Assert.Contains("NoActiveSubscription", hide.Headers.Location!.ToString());
            Assert.False(await InDbAsync(db => db.PerformanceIndicatorExams.AsNoTracking().Where(e => e.Id == id).Select(e => e.IsArchived).SingleAsync()));

            var resend = await PostAsync(a, PartnerAUser, "/Partner/PerformanceIndicatorExams/ResendToStudent", new[]
            {
                Field("examId", id), Field("studentId", _seed.ActiveStudentA)
            }, cookies);
            Assert.Contains("NoActiveSubscription", resend.Headers.Location!.ToString());
        }

        [Fact]
        public async Task PartnerPosts_WithoutAntiForgeryToken_AreRejected()
        {
            var id = await SendCopyToAAsync();
            var (a, cookies) = await PartnerAsync(PartnerAUser, _seed.PartnerA);

            foreach (var url in new[]
            {
                $"/Partner/PerformanceIndicatorExams/Hide/{id}",
                $"/Partner/PerformanceIndicatorExams/Restore/{id}",
                $"/Partner/PerformanceIndicatorExams/ResendToStudent?examId={id}&studentId={_seed.ActiveStudentA}"
            })
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()) };
                request.Headers.Add("Cookie", cookies);
                Assert.Equal(HttpStatusCode.BadRequest, (await a.SendAsync(request)).StatusCode);
            }

            Assert.False(await InDbAsync(db => db.PerformanceIndicatorExams.AsNoTracking().Where(e => e.Id == id).Select(e => e.IsArchived).SingleAsync()));
        }

        [Fact]
        public async Task StudentExamQuestions_BeforeCompletion_RedirectsWithoutAnswers()
        {
            var id = await SendCopyToAAsync();
            var (a, cookies) = await PartnerAsync(PartnerAUser, _seed.PartnerA);

            var response = await GetAsync(a, $"/Partner/PerformanceIndicatorExams/StudentExamQuestions?examId={id}&studentId={_seed.ActiveStudentA}", cookies);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("ExamStudents", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Sidebar_LinksToPerformancePage()
        {
            var (a, cookies) = await PartnerAsync(PartnerAUser, _seed.PartnerA);
            var html = await HtmlAsync(await GetAsync(a, "/Partner/PerformanceIndicatorExams", cookies));

            Assert.Contains("href=\"/Partner/PerformanceIndicatorExams\"", html);
        }

        // =====================================================
        // الطالب
        // =====================================================
        [Fact]
        public async Task Student_NotAssigned_CannotEnterSchoolCopy_NorIsEnrolled()
        {
            var id = await SendCopyToAAsync();
            var code = await InDbAsync(db => db.PerformanceIndicatorExams.AsNoTracking().Where(e => e.Id == id).Select(e => e.ReferenceCode).SingleAsync());
            var studentUser = await InDbAsync(db => db.Students.AsNoTracking().Where(s => s.StudentID == _kpi.StudentB).Select(s => s.UserId).SingleAsync());

            var client = Client(studentUser!, "Student");
            var verified = await PostAsync(client, studentUser!, "/Students/PerformanceIndicatorExams/VerifyReferenceCode", new[]
            {
                Field("id", id), Field("referenceCode", code!)
            });
            Assert.Contains("StartPerformanceExam", verified.Headers.Location!.ToString());

            var response = await GetAsync(client, $"/Students/PerformanceIndicatorExams/StartPerformanceExam/{id}", CookiesOf(verified));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(await InDbAsync(db => db.PerformanceIndicatorExamStudents.AnyAsync(s => s.PerformanceIndicatorExamId == id && s.StudentId == _kpi.StudentB)));
        }

        [Fact]
        public async Task Student_Assigned_EntersSchoolCopy()
        {
            var id = await SendCopyToAAsync();
            var code = await InDbAsync(db => db.PerformanceIndicatorExams.AsNoTracking().Where(e => e.Id == id).Select(e => e.ReferenceCode).SingleAsync());
            var studentUser = await InDbAsync(db => db.Students.AsNoTracking().Where(s => s.StudentID == _seed.ActiveStudentA).Select(s => s.UserId).SingleAsync());

            var client = Client(studentUser!, "Student");
            var verified = await PostAsync(client, studentUser!, "/Students/PerformanceIndicatorExams/VerifyReferenceCode", new[]
            {
                Field("id", id), Field("referenceCode", code!)
            });

            var response = await GetAsync(client, $"/Students/PerformanceIndicatorExams/StartPerformanceExam/{id}", CookiesOf(verified));

            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.NotNull(await InDbAsync(db => db.PerformanceIndicatorExamStudents.AsNoTracking()
                .Where(s => s.PerformanceIndicatorExamId == id && s.StudentId == _seed.ActiveStudentA)
                .Select(s => s.StartedAt)
                .SingleAsync()));
        }
    }
}
