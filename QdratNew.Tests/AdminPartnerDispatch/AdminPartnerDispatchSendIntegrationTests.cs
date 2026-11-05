using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S2.2 + S2.3 — عبر HTTP حقيقي: الجمهور في الشاشتين، الصلاحيات، النتيجة، إخفاء المملوك عن الأدمن، وإدارة الشريك له.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class AdminPartnerDispatchSendIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Base = "/Admin/AdminLessonCompletions";
        private const string OwnerUser = "apds1-owner";
        private const string NoSendUser = "apds1-no-send";
        private const string SendNoOverrideUser = "apds1-send-no-override";
        private const string PartnerAUser = "apds1-partner-a";
        private const string PartnerBUser = "apds1-partner-b";
        private static readonly string HomeworkTitle = $"{ApdTestData.Marker} واجب الشركاء";
        private static readonly string ExamTitle = $"{ApdTestData.Marker} اختبار الشركاء";

        private readonly CustomWebApplicationFactory _factory;
        private ApdSeed _seed = null!;
        private int _examModelId;

        public AdminPartnerDispatchSendIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ApdTestData.CleanupAsync(db);
            _seed = await ApdTestData.SeedAsync(db);
            _examModelId = await ApdTestData.PrepareSendableModelsAsync(db, _seed);

            db.Users.AddRange(ApdTestData.NewUser(OwnerUser), ApdTestData.NewUser(PartnerAUser), ApdTestData.NewUser(PartnerBUser));
            db.UserPartners.AddRange(
                new UserPartner { UserId = PartnerAUser, PartnerId = _seed.PartnerA },
                new UserPartner { UserId = PartnerBUser, PartnerId = _seed.PartnerB });
            await db.SaveChangesAsync();

            await AddProfileUserAsync(db, NoSendUser, ("AdminLessonCompletions", new[] { "ManageModelHomework", "ManageModelExam" }));
            await AddProfileUserAsync(db, SendNoOverrideUser,
                ("AdminLessonCompletions", new[] { "ManageModelHomework", "ManageModelExam" }),
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

        private static string SaudiLocal(int days) => DateTime.UtcNow.AddHours(3).AddDays(days).ToString("yyyy-MM-ddTHH:mm");

        private static async Task<string> HtmlAsync(HttpResponseMessage response)
            => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        private List<KeyValuePair<string, string>> PartnersHomeworkForm(params KeyValuePair<string, string>[] extra)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                Field("ModelId", _seed.ModelLinkedToA),
                Field("Title", HomeworkTitle),
                Field("Audience", (int)DispatchAudience.Partners),
                Field($"PartnerBatchIds[{_seed.PartnerA}]", _seed.BatchA1),
                Field($"PartnerBatchIds[{_seed.PartnerB}]", _seed.BatchB1),
                Field("StartAt", SaudiLocal(0)),
                Field("EndAt", SaudiLocal(2))
            };
            fields.AddRange(extra);
            return fields;
        }

        private List<KeyValuePair<string, string>> PartnersExamForm(params KeyValuePair<string, string>[] extra)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                Field("ModelId", _examModelId),
                Field("Title", ExamTitle),
                Field("Audience", (int)DispatchAudience.Partners),
                Field($"PartnerBatchIds[{_seed.PartnerA}]", _seed.BatchA1),
                Field($"PartnerBatchIds[{_seed.PartnerB}]", _seed.BatchB1),
                Field("SendToBatch", "true"),
                Field("DurationMinutes", 40),
                Field("StartAt", SaudiLocal(0)),
                Field("EndAt", SaudiLocal(2))
            };
            fields.AddRange(extra);
            return fields;
        }

        private async Task<T> ReadAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private Task<int> SetCountAsync() => ReadAsync(db => db.HomeworkSets.AsNoTracking()
            .CountAsync(h => h.BatchId == _seed.BatchA1 || h.BatchId == _seed.BatchB1 || h.BatchId == _seed.BatchI1));

        // يرسل واجبًا واختبارًا للمدرستين ويرجع واجب واختبار المدرسة A
        private async Task<(int SetId, int AssignmentId)> SendToBothAsync()
        {
            var homework = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", PartnersHomeworkForm());
            Assert.Equal(HttpStatusCode.Redirect, homework.StatusCode);

            var exam = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelExam", PartnersExamForm(Field("ConfirmWarnings", "true")));
            Assert.Equal(HttpStatusCode.Redirect, exam.StatusCode);

            return await ReadAsync(async db => (
                await db.HomeworkSets.AsNoTracking().Where(h => h.BatchId == _seed.BatchA1 && h.Title == HomeworkTitle).Select(h => h.Id).SingleAsync(),
                await db.ExamAssignmentsToBatches.AsNoTracking().Where(a => a.BatchId == _seed.BatchA1 && a.Title == ExamTitle).Select(a => a.Id).SingleAsync()));
        }

        // =====================================================
        // الشاشتان
        // =====================================================
        [Theory]
        [InlineData("CreateModelHomework")]
        [InlineData("CreateModelExam")]
        public async Task Get_RendersPicker_PlatformBatchesOnly(string action)
        {
            var html = await HtmlAsync(await Owner().GetAsync($"{Base}/{action}"));

            Assert.Contains("data-apd-picker", html);
            Assert.Contains($"id=\"apd-platform-{_seed.BatchI1}\"", html);
            Assert.DoesNotContain($"id=\"apd-platform-{_seed.BatchA1}\"", html);
            Assert.Contains("admin-dispatch-audience.js", html);
        }

        [Fact]
        public async Task Get_WithoutSendPermission_HidesPartnersOption()
        {
            var html = await HtmlAsync(await Client(NoSendUser, "Admin").GetAsync($"{Base}/CreateModelHomework"));

            Assert.Contains("data-apd-picker", html);
            Assert.DoesNotContain("المدارس الشريكة</strong>", html);
        }

        [Fact]
        public async Task Get_WithBatchId_PreselectsPlatformBatch()
        {
            var html = await HtmlAsync(await Owner().GetAsync($"{Base}/CreateModelHomework?batchId={_seed.BatchI1}"));
            Assert.Matches($"<input[^>]*id=\"apd-platform-{_seed.BatchI1}\"[^>]*checked", html);
        }

        // =====================================================
        // الإرسال للشركاء
        // =====================================================
        [Fact]
        public async Task Homework_ToTwoSchools_RedirectsToResult_EachOwnsCopy_HiddenFromAdmin()
        {
            var response = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", PartnersHomeworkForm());

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/PartnerDispatch/Result", response.Headers.Location!.ToString());

            var result = await GetAsync(Owner(), "/Admin/PartnerDispatch/Result", CookiesOf(response));
            var resultHtml = await HtmlAsync(result);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Contains($"{ApdTestData.Marker} مدرسة A", resultHtml);
            Assert.Contains($"{ApdTestData.Marker} مدرسة B", resultHtml);
            Assert.DoesNotContain("تعذر", resultHtml);

            var owners = await ReadAsync(db => db.HomeworkSets.AsNoTracking().Where(h => h.Title == HomeworkTitle).Select(h => h.OwnerPartnerId).ToListAsync());
            Assert.Equal(2, owners.Count);
            Assert.Contains(_seed.PartnerA, owners.Select(o => o!.Value));
            Assert.Contains(_seed.PartnerB, owners.Select(o => o!.Value));

            Assert.DoesNotContain(HomeworkTitle, await HtmlAsync(await Owner().GetAsync($"{Base}/ModelAssignments")));
            Assert.DoesNotContain(HomeworkTitle, await HtmlAsync(await Owner().GetAsync($"{Base}/BatchModelHomeworks?batchId={_seed.BatchA1}")));
            Assert.DoesNotContain(HomeworkTitle, await HtmlAsync(await Owner().GetAsync($"/Admin/ExamAssignments/ModelAssignments")));
        }

        [Fact]
        public async Task Exam_ToTwoSchools_OwnedPerSchool_HiddenFromAdmin()
        {
            var response = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelExam", PartnersExamForm());

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/PartnerDispatch/Result", response.Headers.Location!.ToString());

            var owners = await ReadAsync(db => db.Exams.AsNoTracking().Where(e => e.SourceProfessionalModelId == _examModelId).Select(e => e.OwnerPartnerId).ToListAsync());
            Assert.Equal(2, owners.Count);
            Assert.DoesNotContain(null, owners);

            Assert.DoesNotContain(ExamTitle, await HtmlAsync(await Owner().GetAsync($"{Base}/ModelAssignments")));
        }

        [Fact]
        public async Task Exam_ToSchools_RejectsSelectedStudentsMode()
        {
            var form = PartnersExamForm();
            form.RemoveAll(f => f.Key == "SendToBatch");
            form.Add(Field("SendToBatch", "false"));

            var response = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelExam", form);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("طلاب محددين", await HtmlAsync(response));
            Assert.False(await ReadAsync(db => db.Exams.AsNoTracking().AnyAsync(e => e.SourceProfessionalModelId == _examModelId)));
        }

        [Fact]
        public async Task Homework_ToSchools_RejectsLectureLink()
        {
            var response = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", PartnersHomeworkForm(Field("IsLinkedToLecture", "true"), Field("LectureId", 1)));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("بمحاضرة", await HtmlAsync(response));
            Assert.Equal(0, await SetCountAsync());
        }

        [Fact]
        public async Task RepeatSend_ShowsOneWarningTable_WithSchoolNames_KeepsSelection()
        {
            Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", PartnersHomeworkForm())).StatusCode);

            await ReadAsync(db => db.HomeworkSets.Where(h => h.Title == HomeworkTitle)
                .ExecuteUpdateAsync(x => x.SetProperty(h => h.CreatedAt, DateTime.UtcNow.AddDays(-1))));

            var warned = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", PartnersHomeworkForm());
            var html = await HtmlAsync(warned);

            Assert.Equal(HttpStatusCode.OK, warned.StatusCode);
            Assert.Contains("modelSendWarnings", html);
            Assert.Contains($"{ApdTestData.Marker} مدرسة A —", html.Replace("&mdash;", "—"));
            Assert.Matches($"<input[^>]*id=\"apd-partner-{_seed.PartnerA}\"[^>]*checked", html);
            Assert.Matches($"<input[^>]*name=\"PartnerBatchIds\\[{_seed.PartnerA}\\]\"[^>]*value=\"{_seed.BatchA1}\"[^>]*checked", html);
            Assert.Equal(2, await SetCountAsync());
        }

        // =====================================================
        // الصلاحيات والتحقق
        // =====================================================
        [Fact]
        public async Task CraftedPartnersRequest_WithoutSendPermission_IsRejected_NoWrites()
        {
            var response = await PostAsync(Client(NoSendUser, "Admin"), NoSendUser, $"{Base}/CreateModelHomework", PartnersHomeworkForm());

            Assert.DoesNotContain("/PartnerDispatch/Result", response.Headers.Location?.ToString() ?? "");
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, await SetCountAsync());
            Assert.False(await ReadAsync(db => db.PartnerDispatchLogs.AsNoTracking().AnyAsync(l => l.PartnerId == _seed.PartnerA)));
        }

        [Theory]
        [InlineData("PlatformBatchIds")]
        [InlineData("SelectedBatchIds")]
        public async Task Platform_RejectsPartnerBatch_FromEitherField(string fieldName)
        {
            var response = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", new[]
            {
                Field("ModelId", _seed.ModelLinkedToA),
                Field("Title", HomeworkTitle),
                Field(fieldName, _seed.BatchA1),
                Field("StartAt", SaudiLocal(0)),
                Field("EndAt", SaudiLocal(2))
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("ليست من دفعات المنصة", await HtmlAsync(response));
            Assert.Equal(0, await SetCountAsync());
        }

        [Fact]
        public async Task Partners_RejectsPlatformBatch_AndForeignBatch()
        {
            var withPlatform = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", PartnersHomeworkForm(Field("PlatformBatchIds", _seed.BatchI1)));
            Assert.Contains("دفعات المنصة", await HtmlAsync(withPlatform));

            var foreign = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", new[]
            {
                Field("ModelId", _seed.ModelLinkedToA),
                Field("Title", HomeworkTitle),
                Field("Audience", (int)DispatchAudience.Partners),
                Field($"PartnerBatchIds[{_seed.PartnerA}]", _seed.BatchB1),
                Field("StartAt", SaudiLocal(0)),
                Field("EndAt", SaudiLocal(2))
            });
            Assert.Contains("لا تخص هذه المدرسة", await HtmlAsync(foreign));
            Assert.Equal(0, await SetCountAsync());
        }

        [Fact]
        public async Task PlatformChoice_StillWorks_ThroughPickerField()
        {
            var response = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", new[]
            {
                Field("ModelId", _seed.ModelLinkedToA),
                Field("Title", HomeworkTitle),
                Field("Audience", (int)DispatchAudience.Platform),
                Field("PlatformBatchIds", _seed.BatchI1),
                Field("StartAt", SaudiLocal(0)),
                Field("EndAt", SaudiLocal(2))
            });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("ModelAssignments", response.Headers.Location!.ToString());
            Assert.Null(await ReadAsync(db => db.HomeworkSets.AsNoTracking().Where(h => h.BatchId == _seed.BatchI1).Select(h => h.OwnerPartnerId).SingleAsync()));
            Assert.Contains(HomeworkTitle, await HtmlAsync(await Owner().GetAsync($"{Base}/ModelAssignments")));
        }

        [Fact]
        public async Task Override_NeedsPermissionAndReason_AndIsLogged()
        {
            var form = new[]
            {
                Field("ModelId", _seed.ModelNotForAnyone),
                Field("Title", HomeworkTitle),
                Field("Audience", (int)DispatchAudience.Partners),
                Field($"PartnerBatchIds[{_seed.PartnerA}]", _seed.BatchA1),
                Field("StartAt", SaudiLocal(0)),
                Field("EndAt", SaudiLocal(2))
            };

            // نموذج غير متاح بلا تجاوز
            var blocked = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", form);
            Assert.Contains("النموذج غير متاح", await HtmlAsync(blocked));

            var reason = "اتفاق خاص مع إدارة المدرسة";
            var overrideFields = form.Concat(new[] { Field("OverrideContract", "true"), Field("OverrideReason", reason) }).ToList();

            // بلا صلاحية Override
            var denied = await PostAsync(Client(SendNoOverrideUser, "Admin"), SendNoOverrideUser, $"{Base}/CreateModelHomework", overrideFields);
            Assert.Contains("لا تملك صلاحية تجاوز", await HtmlAsync(denied));

            // سبب قصير
            var shortReason = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", form.Concat(new[] { Field("OverrideContract", "true"), Field("OverrideReason", "قصير") }));
            Assert.Contains("سبب تجاوز العقد", await HtmlAsync(shortReason));
            Assert.Equal(0, await SetCountAsync());

            var sent = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", overrideFields);
            Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);

            var log = await ReadAsync(db => db.PartnerDispatchLogs.AsNoTracking().SingleAsync(l => l.PartnerId == _seed.PartnerA));
            Assert.True(log.ContractOverridden);
            Assert.Equal(reason, log.OverrideReason);
        }

        [Fact]
        public async Task DisabledSwitch_RejectsPartnersRequest()
        {
            await ReadAsync(async db =>
            {
                await db.SystemSettings.Where(s => s.Key == "PartnerDispatch.Enabled").ExecuteDeleteAsync();
                db.SystemSettings.Add(new SystemSetting { Key = "PartnerDispatch.Enabled", Value = "false", Description = "-" });
                return await db.SaveChangesAsync();
            });

            try
            {
                var response = await PostAsync(Owner(), OwnerUser, $"{Base}/CreateModelHomework", PartnersHomeworkForm());
                Assert.DoesNotContain("/PartnerDispatch/Result", response.Headers.Location?.ToString() ?? "");
                Assert.Equal(0, await SetCountAsync());
            }
            finally
            {
                await ReadAsync(db => db.SystemSettings.Where(s => s.Key == "PartnerDispatch.Enabled").ExecuteDeleteAsync());
            }
        }

        [Fact]
        public async Task Result_WithoutOutcome_RedirectsToList()
        {
            var response = await Owner().GetAsync("/Admin/PartnerDispatch/Result");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("ModelAssignments", response.Headers.Location!.ToString());
        }

        // =====================================================
        // الأدمن لا يدير ما يملكه الشريك
        // =====================================================
        [Fact]
        public async Task Admin_DirectIds_OfOwnedItems_AreNotFound_AndUnchanged()
        {
            var (setId, assignmentId) = await SendToBothAsync();

            var client = Owner();
            foreach (var url in new[]
            {
                $"{Base}/EditProfessionalHomework/{setId}",
                $"{Base}/EditProfessionalExam/{assignmentId}",
                $"/Admin/HomeworkManagement/Details/{setId}",
                $"/Admin/HomeworkManagement/StudentsReport?homeworkSetId={setId}",
                $"/Admin/HomeworkManagement/ManageHomeworkQuestions?homeworkSetId={setId}",
                $"/Admin/HomeworkManagement/ArchiveHistory?homeworkSetId={setId}",
                $"/Admin/Batches/HomeworkSetDetail?homeworkSetId={setId}&batchId={_seed.BatchA1}"
            })
            {
                // بعض الأكشنات القديمة تعيد التوجيه برسالة بدل 404، والمهم ألا تُعرض الصفحة
                var status = (await client.GetAsync(url)).StatusCode;
                Assert.True(status is HttpStatusCode.NotFound or HttpStatusCode.Redirect, $"{url}: {status}");
            }

            Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(client, OwnerUser, $"{Base}/EditProfessionalHomework", new[]
            {
                Field("Id", setId), Field("Title", "تعديل مرفوض"), Field("BatchId", _seed.BatchA1), Field("BatchName", "-"), Field("DurationMinutes", 30)
            })).StatusCode);

            foreach (var (url, id) in new[]
            {
                ($"{Base}/ArchiveModelHomework", setId),
                ($"{Base}/DeleteModelHomework", setId),
                ("/Admin/HomeworkManagement/ArchiveHomeworkSet", setId),
                ("/Admin/HomeworkManagement/DeleteHomeworkSet", setId)
            })
            {
                await PostAsync(client, OwnerUser, url, new[] { Field("id", id) });
            }

            await PostAsync(client, OwnerUser, $"{Base}/ArchiveModelItemsByBatches", new[] { Field("selectedBatchIds", _seed.BatchA1) });
            await PostAsync(client, OwnerUser, "/Admin/HomeworkManagement/ArchiveHomeworkSetsByBatches", new[] { Field("selectedBatchIds", _seed.BatchA1) });

            var set = await ReadAsync(db => db.HomeworkSets.AsNoTracking().SingleAsync(h => h.Id == setId));
            Assert.Equal(HomeworkTitle, set.Title);
            Assert.False(set.IsArchived);
        }

        [Fact]
        public async Task Admin_Lists_HideOwned_ButKeepUnownedOnPartnerBatch()
        {
            await SendToBothAsync();

            // واجب قديم من المعهد على دفعة شريك، بلا مالك: يبقى ظاهرًا
            var legacyTitle = $"{ApdTestData.Marker} واجب قديم";
            await ReadAsync(async db =>
            {
                db.HomeworkSets.Add(new HomeworkSet
                {
                    BatchId = _seed.BatchA1, Title = legacyTitle, CompletionTitle = legacyTitle, CreatedAt = DateTime.UtcNow,
                    IsFromProfessionalModel = true, IsSent = true, AssignedByUserId = OwnerUser
                });
                return await db.SaveChangesAsync();
            });

            var client = Owner();
            foreach (var url in new[] { $"{Base}/ModelAssignments", $"{Base}/BatchModelHomeworks?batchId={_seed.BatchA1}", $"/Admin/HomeworkManagement/BatchHomeworks?batchId={_seed.BatchA1}" })
            {
                var html = await HtmlAsync(await client.GetAsync(url));
                Assert.Contains(legacyTitle, html);
                Assert.DoesNotContain(HomeworkTitle, html);
            }
        }

        // =====================================================
        // الشريك يدير ما أصبح ملكه
        // =====================================================
        [Fact]
        public async Task Partner_SeesInstituteBadge_InBothLists_OtherPartnerSeesOnlyOwn()
        {
            await SendToBothAsync();

            var (clientA, cookiesA) = await PartnerAsync(PartnerAUser, _seed.PartnerA);

            var homeworks = await HtmlAsync(await GetAsync(clientA, "/Partner/SentHomeworks", cookiesA));
            Assert.Contains(HomeworkTitle, homeworks);
            Assert.Contains("مرسل من المعهد", homeworks);

            var exams = await HtmlAsync(await GetAsync(clientA, "/Partner/PartnerExam/SentExams", cookiesA));
            Assert.Contains(ExamTitle, exams);
            Assert.Contains("مرسل من المعهد", exams);
        }

        [Fact]
        public async Task Partner_OwnDraftHomework_HasNoInstituteBadge()
        {
            var title = $"{ApdTestData.Marker} واجب الشريك نفسه";
            await ReadAsync(async db =>
            {
                db.HomeworkSets.Add(new HomeworkSet
                {
                    BatchId = _seed.BatchA1, Title = title, CompletionTitle = title, CreatedAt = DateTime.UtcNow,
                    OwnerPartnerId = _seed.PartnerA, IsSent = true, AssignedByUserId = PartnerAUser
                });
                return await db.SaveChangesAsync();
            });

            var (client, cookies) = await PartnerAsync(PartnerAUser, _seed.PartnerA);
            var html = await HtmlAsync(await GetAsync(client, "/Partner/SentHomeworks", cookies));

            Assert.Contains(title, html);
            Assert.DoesNotContain("مرسل من المعهد", html);
        }

        [Fact]
        public async Task Partner_EditsOwnedHomework_DatesConvertedToUtc_OtherPartnerNotFound()
        {
            var (setId, _) = await SendToBothAsync();

            var (clientA, cookiesA) = await PartnerAsync(PartnerAUser, _seed.PartnerA);
            Assert.Equal(HttpStatusCode.OK, (await GetAsync(clientA, $"/Partner/SentHomeworks/EditSent/{setId}", cookiesA)).StatusCode);

            var (clientB, cookiesB) = await PartnerAsync(PartnerBUser, _seed.PartnerB);
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(clientB, $"/Partner/SentHomeworks/EditSent/{setId}", cookiesB)).StatusCode);

            var denied = await PostAsync(clientB, PartnerBUser, "/Partner/SentHomeworks/EditSent", new[]
            {
                Field("Id", setId), Field("Title", "تعديل مرفوض"), Field("StartAtLocal", "2030-01-01T10:00"), Field("EndAtLocal", "2030-01-02T10:00")
            }, cookiesB);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);

            var invalid = await PostAsync(clientA, PartnerAUser, "/Partner/SentHomeworks/EditSent", new[]
            {
                Field("Id", setId), Field("Title", "عنوان"), Field("StartAtLocal", "2030-01-02T10:00"), Field("EndAtLocal", "2030-01-01T10:00")
            }, cookiesA);
            Assert.Contains("بعد موعد البداية", await HtmlAsync(invalid));

            var saved = await PostAsync(clientA, PartnerAUser, "/Partner/SentHomeworks/EditSent", new[]
            {
                Field("Id", setId), Field("Title", $"{ApdTestData.Marker} معدل"), Field("StartAtLocal", "2030-01-01T10:00"), Field("EndAtLocal", "2030-01-02T10:00")
            }, cookiesA);
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

            var set = await ReadAsync(db => db.HomeworkSets.AsNoTracking().SingleAsync(h => h.Id == setId));
            Assert.Equal($"{ApdTestData.Marker} معدل", set.Title);
            Assert.Equal(new DateTime(2030, 1, 1, 7, 0, 0), set.StartAt);
            Assert.Equal(new DateTime(2030, 1, 2, 7, 0, 0), set.EndAt);
        }

        [Fact]
        public async Task Partner_EditsOwnedExam_UpdatesAssignmentAndExam_OtherPartnerNotFound()
        {
            var (_, assignmentId) = await SendToBothAsync();

            var (clientB, cookiesB) = await PartnerAsync(PartnerBUser, _seed.PartnerB);
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(clientB, $"/Partner/PartnerExam/EditSent/{assignmentId}", cookiesB)).StatusCode);

            var (clientA, cookiesA) = await PartnerAsync(PartnerAUser, _seed.PartnerA);
            var saved = await PostAsync(clientA, PartnerAUser, "/Partner/PartnerExam/EditSent", new[]
            {
                Field("Id", assignmentId), Field("Title", $"{ApdTestData.Marker} اختبار معدل"), Field("StartAtLocal", "2030-01-01T10:00"), Field("EndAtLocal", "2030-01-02T10:00")
            }, cookiesA);
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

            var assignment = await ReadAsync(db => db.ExamAssignmentsToBatches.AsNoTracking().Include(a => a.Exam).SingleAsync(a => a.Id == assignmentId));
            Assert.Equal($"{ApdTestData.Marker} اختبار معدل", assignment.Title);
            Assert.Equal($"{ApdTestData.Marker} اختبار معدل", assignment.Exam.Title);
            Assert.Equal(new DateTime(2030, 1, 1, 7, 0, 0), assignment.ScheduledDate);
        }

        [Fact]
        public async Task Partner_CannotEdit_UnownedHomeworkOnItsBatch()
        {
            var legacyTitle = $"{ApdTestData.Marker} واجب قديم";
            var setId = await ReadAsync(async db =>
            {
                var set = new HomeworkSet
                {
                    BatchId = _seed.BatchA1, Title = legacyTitle, CompletionTitle = legacyTitle, CreatedAt = DateTime.UtcNow,
                    IsSent = true, AssignedByUserId = OwnerUser
                };
                db.HomeworkSets.Add(set);
                await db.SaveChangesAsync();
                return set.Id;
            });

            var (client, cookies) = await PartnerAsync(PartnerAUser, _seed.PartnerA);
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(client, $"/Partner/SentHomeworks/EditSent/{setId}", cookies)).StatusCode);
        }
    }
}
