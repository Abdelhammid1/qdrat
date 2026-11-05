using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Placement;
using QdratNew.Services.PartnerDispatch;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S3.1 + S3.2 — عبر HTTP حقيقي: الجمهور في شاشة تحديد المستوى، الصلاحيات، صفحة النتيجة، الإخفاء عن الأدمن، ووسم الشريك.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class AdminPartnerPlacementDispatchIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Url = "/Admin/PlacementExams/CreateFromProfessionalModel";
        private const string OwnerUser = "apds1-owner";
        private const string NoSendUser = "apds1-no-send";
        private const string SendNoOverrideUser = "apds1-send-no-override";
        private const string PartnerAUser = "apds1-partner-a";
        private const string PartnerBUser = "apds1-partner-b";

        private readonly CustomWebApplicationFactory _factory;
        private ApdSeed _seed = null!;
        private ApdPlacementSeed _placement = null!;

        public AdminPartnerPlacementDispatchIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ApdTestData.CleanupAsync(db);
            _seed = await ApdTestData.SeedAsync(db);
            _placement = await ApdTestData.PreparePlacementAsync(db, _seed);

            db.Users.AddRange(ApdTestData.NewUser(OwnerUser), ApdTestData.NewUser(PartnerAUser), ApdTestData.NewUser(PartnerBUser));
            db.UserPartners.AddRange(
                new UserPartner { UserId = PartnerAUser, PartnerId = _seed.PartnerA },
                new UserPartner { UserId = PartnerBUser, PartnerId = _seed.PartnerB });
            await db.SaveChangesAsync();

            await AddProfileUserAsync(db, NoSendUser, ("PlacementExams", new[] { "Create" }));
            await AddProfileUserAsync(db, SendNoOverrideUser,
                ("PlacementExams", new[] { "Create" }),
                ("PartnerDispatch", new[] { "Send" }));
        }

        public async Task DisposeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            await ApdTestData.CleanupAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private static async Task AddProfileUserAsync(ApplicationDbContext db, string userId, params (string Controller, string[] Actions)[] permissions)
        {
            db.Users.Add(ApdTestData.NewUser(userId));
            var profile = new AdminProfile { Name = $"{ApdTestData.Marker} {userId}" };
            db.AdminProfiles.Add(profile);
            await db.SaveChangesAsync();

            foreach (var permission in permissions)
            {
                var row = new AdminProfileControllerPermission
                {
                    AdminProfileId = profile.Id,
                    ControllerName = permission.Controller,
                    AccessLevel = AdminControllerAccessLevel.Read,
                    HasCustomPermissions = true
                };
                foreach (var action in permission.Actions)
                    row.CustomPermissions.Add(new AdminProfileCustomPermission { ActionName = action, IsAllowed = true });
                db.AdminProfileControllerPermissions.Add(row);
            }

            db.AdminUserProfiles.Add(new AdminUserProfile { UserId = userId, AdminProfileId = profile.Id });
            await db.SaveChangesAsync();
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

        private List<KeyValuePair<string, string>> PartnersForm(params KeyValuePair<string, string>[] extra)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                Field("SelectedModelId", _placement.ModelId),
                Field("SelectedCourseId", _seed.CourseId),
                Field("DurationMinutes", 45),
                Field("Audience", (int)DispatchAudience.Partners),
                Field($"PartnerBatchIds[{_seed.PartnerA}]", _seed.BatchA1),
                Field($"PartnerBatchIds[{_seed.PartnerA}]", _placement.BatchA4),
                Field($"PartnerBatchIds[{_seed.PartnerB}]", _seed.BatchB1)
            };
            fields.AddRange(extra);
            return fields;
        }

        private List<KeyValuePair<string, string>> PlatformForm(params KeyValuePair<string, string>[] extra)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                Field("SelectedModelId", _placement.ModelId),
                Field("SelectedCourseId", _seed.CourseId),
                Field("DurationMinutes", 45)
            };
            fields.AddRange(extra);
            return fields;
        }

        private async Task<T> ReadAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private Task<Exam?> ExamOfAsync(int partnerId)
            => ReadAsync(db => db.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.OwnerPartnerId == partnerId));

        private Task<int> ExamCountAsync(params int[] batchIds)
            => ReadAsync(db => db.ExamAssignmentsToBatches.AsNoTracking().CountAsync(l => batchIds.Contains(l.BatchId)));

        // =====================================================
        // الشاشة
        // =====================================================
        [Fact]
        public async Task Get_RendersPicker_PlatformBatchesAsRadios_NoPartnerBatch()
        {
            var html = await HtmlAsync(await Owner().GetAsync(Url));

            Assert.Contains("data-apd-picker", html);
            Assert.Contains("data-apd-kind=\"3\"", html);
            Assert.Contains("data-apd-requires-course=\"1\"", html);
            Assert.Contains("id=\"partnerDeliverySection\"", html);
            Assert.Contains("المدارس الشريكة", html);
            Assert.Matches($"<input[^>]*type=\"radio\"[^>]*id=\"apd-platform-{_seed.BatchI1}\"", html);
            Assert.DoesNotContain($"apd-platform-{_seed.BatchA1}\"", html);
            Assert.DoesNotContain("name=\"SelectedBatchId\"", html);
        }

        [Fact]
        public async Task Get_WithoutSendPermission_HidesPartnersOption()
        {
            var html = await HtmlAsync(await Client(NoSendUser, "Admin").GetAsync(Url));

            Assert.Contains("data-apd-picker", html);
            Assert.DoesNotContain("المدارس الشريكة</strong>", html);
        }

        [Fact]
        public async Task AudienceOptions_FiltersSchoolBatchesByCourse()
        {
            var owner = Owner();

            var sameCourse = await HtmlAsync(await owner.GetAsync($"/Admin/PartnerDispatch/AudienceOptions?kind=3&modelId={_placement.ModelId}&courseId={_seed.CourseId}"));
            Assert.Contains($"apd-batch-{_seed.PartnerA}-{_seed.BatchA1}\"", sameCourse);
            Assert.DoesNotContain($"apd-batch-{_seed.PartnerA}-{_seed.BatchA3OtherCurriculum}\"", sameCourse);

            var otherCourse = await HtmlAsync(await owner.GetAsync($"/Admin/PartnerDispatch/AudienceOptions?kind=3&modelId={_placement.ModelId}&courseId={_seed.OtherCourseId}"));
            Assert.DoesNotContain($"apd-batch-{_seed.PartnerA}-{_seed.BatchA1}\"", otherCourse);
        }

        // =====================================================
        // «المنصة»
        // =====================================================
        [Fact]
        public async Task Platform_InstituteBatch_BehavesAsBefore_OwnedByInstitute()
        {
            var response = await PostAsync(Owner(), OwnerUser, Url, PlatformForm(Field("PlatformBatchIds", _seed.BatchI1)));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.EndsWith("/PlacementExams", response.Headers.Location!.ToString().TrimEnd('/').Replace("/Index", ""));

            var link = await ReadAsync(db => db.ExamAssignmentsToBatches.AsNoTracking().Include(l => l.Exam).SingleAsync(l => l.BatchId == _seed.BatchI1));
            Assert.Null(link.Exam.OwnerPartnerId);
            Assert.True(link.IsSentToStudents);
            Assert.False(await ReadAsync(db => db.PartnerDispatchLogs.AsNoTracking().AnyAsync(l => l.TargetEntityId == link.ExamId && l.Kind == PartnerDispatchKind.Placement)));
        }

        [Fact]
        public async Task Platform_LegacySelectedBatchId_StillWorks()
        {
            var response = await PostAsync(Owner(), OwnerUser, Url, PlatformForm(Field("SelectedBatchId", _seed.BatchI1)));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(1, await ExamCountAsync(_seed.BatchI1));
        }

        [Theory]
        [InlineData("PlatformBatchIds")]
        [InlineData("SelectedBatchId")]
        public async Task Platform_PartnerBatch_IsRejected_NothingWritten(string fieldName)
        {
            var response = await PostAsync(Owner(), OwnerUser, Url, PlatformForm(Field(fieldName, _seed.BatchA1)));
            var html = await HtmlAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("ليست من دفعات المنصة", html);
            Assert.Equal(0, await ExamCountAsync(_seed.BatchA1));
        }

        [Fact]
        public async Task Platform_TwoBatches_IsRejected_NoBatch_ShowsMessage()
        {
            var two = await HtmlAsync(await PostAsync(Owner(), OwnerUser, Url, PlatformForm(Field("PlatformBatchIds", _seed.BatchI1), Field("PlatformBatchIds", _seed.BatchI2OtherCourse))));
            Assert.Contains("دفعة منصة واحدة فقط", two);
            Assert.Equal(0, await ExamCountAsync(_seed.BatchI1, _seed.BatchI2OtherCourse));

            var none = await HtmlAsync(await PostAsync(Owner(), OwnerUser, Url, PlatformForm()));
            Assert.Contains("يجب اختيار دفعة", none);
        }

        // =====================================================
        // «المدارس الشريكة»
        // =====================================================
        [Fact]
        public async Task Partners_SendNow_ResultPage_EachSchoolOwnsCopy_HiddenFromAdmin_BadgeForSchool()
        {
            var owner = Owner();
            var response = await PostAsync(owner, OwnerUser, Url, PartnersForm());

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/PartnerDispatch/Result", response.Headers.Location!.ToString());

            var result = await HtmlAsync(await GetAsync(owner, response.Headers.Location!.ToString(), CookiesOf(response)));
            Assert.Contains("نتيجة إرسال اختبار تحديد المستوى", result);
            Assert.Contains($"data-apd-result-partner=\"{_seed.PartnerA}\"", result);
            Assert.Contains($"data-apd-result-partner=\"{_seed.PartnerB}\"", result);
            Assert.Contains("/Admin/PlacementExams/CreateFromProfessionalModel", result);

            var examA = await ExamOfAsync(_seed.PartnerA);
            var examB = await ExamOfAsync(_seed.PartnerB);
            Assert.NotNull(examA);
            Assert.NotNull(examB);
            Assert.NotEqual(examA!.Id, examB!.Id);
            Assert.Equal(1, await ReadAsync(db => db.ExamAssignments.AsNoTracking().CountAsync(a => a.ExamId == examB.Id)));

            // الأدمن لا يراها ولا يفتحها
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Admin/PlacementExams/Review?examId={examA.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Admin/PlacementExams/PlacementDetails/{examA.Id}")).StatusCode);
            Assert.DoesNotContain(examA.Title, await HtmlAsync(await owner.GetAsync($"/Admin/PlacementExams/BatchExams?batchId={_seed.BatchA1}")));
            Assert.DoesNotContain(ApdTestData.Marker + " دفعة A1", await HtmlAsync(await owner.GetAsync("/Admin/PlacementExams")));

            // الشريك: وسم من المعهد وربط بدفعتين
            var (clientA, cookiesA) = await PartnerAsync(PartnerAUser, _seed.PartnerA);
            var listA = await HtmlAsync(await GetAsync(clientA, $"/Partner/PlacementExams/BatchExams?batchId={_seed.BatchA1}", cookiesA));
            Assert.Contains(examA.Title, listA);
            Assert.Contains("data-apd-from-institute", listA);
            Assert.Contains("من المعهد", listA);
            Assert.Contains("مرتبط بـ 2 دفعات", listA);
            Assert.Contains("يسري هذا على كل الدفعات المرتبطة (2)", listA);

            // وشريك آخر لا يصل لاختبار A
            var (clientB, cookiesB) = await PartnerAsync(PartnerBUser, _seed.PartnerB);
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(clientB, $"/Partner/PlacementExams/Review?examId={examA.Id}", cookiesB)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(clientB, $"/Partner/PlacementExams/BatchExams?batchId={_seed.BatchA1}", cookiesB)).StatusCode);
        }

        [Fact]
        public async Task Partners_Draft_ResultPageSaysDraft_SchoolSeesDraft_NoStudentAssignment()
        {
            var owner = Owner();
            var response = await PostAsync(owner, OwnerUser, Url, PartnersForm(Field("PartnerDelivery", nameof(PlacementPartnerDelivery.Draft))));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var result = await HtmlAsync(await GetAsync(owner, response.Headers.Location!.ToString(), CookiesOf(response)));
            Assert.Contains("أُنشئت مسودة لدى 2 مدرسة", result);

            var examA = await ExamOfAsync(_seed.PartnerA);
            Assert.NotNull(examA);
            Assert.False(await ReadAsync(db => db.ExamAssignments.AsNoTracking().AnyAsync(a => a.ExamId == examA!.Id)));
            Assert.False(await ReadAsync(db => db.ExamStudentStatuses.AsNoTracking().AnyAsync(s => s.ExamId == examA!.Id)));

            var (clientA, cookiesA) = await PartnerAsync(PartnerAUser, _seed.PartnerA);
            var list = await HtmlAsync(await GetAsync(clientA, $"/Partner/PlacementExams/BatchExams?batchId={_seed.BatchA1}", cookiesA));
            Assert.Contains("مسودة", list);
            Assert.Contains("data-apd-from-institute", list);
            Assert.Contains($"ConfirmSend?examId={examA!.Id}", list);
        }

        [Fact]
        public async Task Partners_SchoolWithoutPlacementContract_NeedsOverride_NoPermission_Rejected()
        {
            await ReadAsync(async db =>
            {
                var contract = await db.PartnerSubscriptions.FirstAsync(s => s.PartnerId == _seed.PartnerA);
                contract.CanUsePlacementExams = false;
                return await db.SaveChangesAsync();
            });

            var blocked = await HtmlAsync(await PostAsync(Owner(), OwnerUser, Url, PartnersForm()));
            Assert.Contains("العقد لا يشمل تحديد المستوى", blocked);
            Assert.Null(await ExamOfAsync(_seed.PartnerB));

            var noOverride = await HtmlAsync(await PostAsync(
                Client(SendNoOverrideUser, "Admin"), SendNoOverrideUser, Url,
                PartnersForm(Field("OverrideContract", "true"), Field("OverrideReason", "موافقة الإدارة على هذا الإرسال"))));
            Assert.Contains("لا تملك صلاحية تجاوز", noOverride);
            Assert.Null(await ExamOfAsync(_seed.PartnerA));

            var overridden = await PostAsync(
                Owner(), OwnerUser, Url,
                PartnersForm(Field("OverrideContract", "true"), Field("OverrideReason", "موافقة الإدارة على هذا الإرسال")));
            Assert.Contains("/PartnerDispatch/Result", overridden.Headers.Location!.ToString());

            var examA = await ExamOfAsync(_seed.PartnerA);
            Assert.NotNull(examA);
            var logs = await ReadAsync(db => db.PartnerDispatchLogs.AsNoTracking().Where(l => l.TargetEntityId == examA!.Id && l.Kind == PartnerDispatchKind.Placement).ToListAsync());
            Assert.Equal(2, logs.Count);
            Assert.All(logs, l => Assert.True(l.ContractOverridden));
        }

        [Fact]
        public async Task Partners_WithoutSendPermission_IsForbidden_NothingWritten()
        {
            var response = await PostAsync(Client(NoSendUser, "Admin"), NoSendUser, Url, PartnersForm());

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Null(await ExamOfAsync(_seed.PartnerA));
        }

        [Fact]
        public async Task Partners_DisabledBySetting_IsForbidden()
        {
            await ReadAsync(async db =>
            {
                db.SystemSettings.Add(new SystemSetting { Key = DispatchAudiencePickerBuilder.EnabledSettingKey, Value = "false" });
                return await db.SaveChangesAsync();
            });

            try
            {
                var response = await PostAsync(Owner(), OwnerUser, Url, PartnersForm());
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                Assert.Null(await ExamOfAsync(_seed.PartnerA));
            }
            finally
            {
                await ReadAsync(db => db.SystemSettings.Where(s => s.Key == DispatchAudiencePickerBuilder.EnabledSettingKey).ExecuteDeleteAsync());
            }
        }

        [Fact]
        public async Task Post_WithoutAntiForgery_IsRejected()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = new FormUrlEncodedContent(PartnersForm()) };
            var response = await Owner().SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Null(await ExamOfAsync(_seed.PartnerA));
        }

        [Fact]
        public async Task Partners_ForgedBatchOrCourse_IsRejected_SelectionKept()
        {
            // دفعة A تحت B
            var foreign = new List<KeyValuePair<string, string>>
            {
                Field("SelectedModelId", _placement.ModelId),
                Field("SelectedCourseId", _seed.CourseId),
                Field("DurationMinutes", 45),
                Field("Audience", (int)DispatchAudience.Partners),
                Field($"PartnerBatchIds[{_seed.PartnerB}]", _seed.BatchA1)
            };
            Assert.Contains("لا تخص هذه المدرسة", await HtmlAsync(await PostAsync(Owner(), OwnerUser, Url, foreign)));

            // دفعة لا تتبع الكورس المختار
            var otherCourse = PartnersForm();
            otherCourse.RemoveAll(f => f.Key == "SelectedCourseId");
            otherCourse.Add(Field("SelectedCourseId", _seed.OtherCourseId));
            Assert.Contains("لا تخص هذه المدرسة", await HtmlAsync(await PostAsync(Owner(), OwnerUser, Url, otherCourse)));

            // دفعة منصة تحت «الشركاء»
            var html = await HtmlAsync(await PostAsync(Owner(), OwnerUser, Url, PartnersForm(Field("PlatformBatchIds", _seed.BatchI1))));
            Assert.Contains("لا يمكن اختيار دفعات المنصة", html);

            // الاختيار يبقى ظاهرًا عند إعادة العرض
            Assert.Matches($"<input[^>]*id=\"apd-partner-{_seed.PartnerA}\"[^>]*checked", html);
            Assert.Null(await ExamOfAsync(_seed.PartnerA));
            Assert.Null(await ExamOfAsync(_seed.PartnerB));
        }

        [Fact]
        public async Task Partners_MissingModelOrCourse_InvalidDelivery_ShowMessages()
        {
            var noCourse = PartnersForm();
            noCourse.RemoveAll(f => f.Key == "SelectedCourseId");
            Assert.Contains("يجب اختيار كورس", await HtmlAsync(await PostAsync(Owner(), OwnerUser, Url, noCourse)));

            var noModel = PartnersForm();
            noModel.RemoveAll(f => f.Key == "SelectedModelId");
            Assert.Contains("يجب اختيار نموذج", await HtmlAsync(await PostAsync(Owner(), OwnerUser, Url, noModel)));

            Assert.Contains("طريقة الإرسال غير صالحة", await HtmlAsync(await PostAsync(Owner(), OwnerUser, Url, PartnersForm(Field("PartnerDelivery", "9")))));
            Assert.Null(await ExamOfAsync(_seed.PartnerA));
        }

        // =====================================================
        // تقارير الاختبار المرتبط بعدة دفعات
        // =====================================================
        [Fact]
        public async Task MultiBatchExam_ReviewBatchReport_UsesRequestedBatch()
        {
            var response = await PostAsync(Owner(), OwnerUser, Url, PartnersForm());
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var examA = await ExamOfAsync(_seed.PartnerA);

            using var scope = _factory.Services.CreateScope();
            var reports = scope.ServiceProvider.GetRequiredService<IPlacementExamReportService>();
            var partner = PlacementScope.Partner(_seed.PartnerA, PartnerAUser);

            var forA1 = await reports.GetReviewLessonQuestionsBatchAsync(partner, examA!.Id, _seed.LessonId, default, _seed.BatchA1);
            var forA4 = await reports.GetReviewLessonQuestionsBatchAsync(partner, examA.Id, _seed.LessonId, default, _placement.BatchA4);

            // في A1 ثلاثة من طلاب A (نشط، غير نشط، منسحب)، وفي A4 طالب واحد، ولا إجابات بعد
            Assert.Equal(3, Assert.Single(forA1!.Questions).SkippedCount);
            Assert.Equal(1, Assert.Single(forA4!.Questions).SkippedCount);

            // دفعة لا تخص الاختبار لا تُقرأ: يُرجَع لدفعة مرتبطة، وطلاب دفعة B لا يُحسبون
            var foreign = await reports.GetReviewLessonQuestionsBatchAsync(partner, examA.Id, _seed.LessonId, default, _seed.BatchB1);
            Assert.Contains(Assert.Single(foreign!.Questions).SkippedCount, new[] { 1, 3 });
        }
    }
}
