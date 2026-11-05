using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Tests.Integration;
using QdratNew.ViewModels.Partner;
using Xunit;

namespace QdratNew.Tests.PartnerProfessionalModels
{
    // PPM-S5 — إرسال اختبار من نموذج عبر HTTP حقيقي: الشاشات والعزل والعقد، ثم الظهور عند الطالب والشريك وعدم الظهور للأدمن.
    [Collection("SharedIntegrationDb")]
    public class PartnerModelExamSendIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Marker = "PPMS5H";
        private const string PartnerUser = "ppm-s5-partner";
        private const string OtherPartnerUser = "ppm-s5-partner-b";
        private const string NoExamsUser = "ppm-s5-partner-c";
        private const string StudentUser = "ppm-s5-student";
        private const string OwnerUser = "ppm-s5-owner";
        private const string Base = "/Partner/ProfessionalModels";
        private const string ModelTitle = "PPMS5H نموذج اختبار";
        private const string HomeworkModelTitle = "PPMS5H نموذج واجب";
        private const string ForeignModelTitle = "PPMS5H نموذج شريك آخر";

        private readonly CustomWebApplicationFactory _factory;
        private int _partnerId;
        private int _otherPartnerId;
        private int _noExamsPartnerId;
        private int _batchId;
        private int _foreignBatchId;
        private int _modelId;
        private int _homeworkModelId;
        private int _foreignModelId;
        private int _studentId;
        private Guid _q1;
        private Guid _substitute;

        public PartnerModelExamSendIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var partner = NewPartner("A");
            var other = NewPartner("B");
            var noExams = NewPartner("C");
            var branch = NewBranch("فرع", partner);
            var otherBranch = NewBranch("فرع آخر", other);
            var noExamsBranch = NewBranch("فرع بلا اختبارات", noExams);
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var curriculum = new Curriculum { Title = $"{Marker} منهج", Description = "-", CurriculumTypeName = "-" };
            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.AddRange(partner, other, noExams, branch, otherBranch, noExamsBranch, course, curriculum, unit);
            db.Users.AddRange(NewUser(PartnerUser), NewUser(OtherPartnerUser), NewUser(NoExamsUser), NewUser(StudentUser), NewUser(OwnerUser));
            await db.SaveChangesAsync();

            db.UserPartners.AddRange(
                new UserPartner { UserId = PartnerUser, PartnerId = partner.Id },
                new UserPartner { UserId = OtherPartnerUser, PartnerId = other.Id },
                new UserPartner { UserId = NoExamsUser, PartnerId = noExams.Id });

            var contract = NewContract(partner.Id, canCreateExams: true, canUsePlacement: true);
            var otherContract = NewContract(other.Id, canCreateExams: true, canUsePlacement: false);
            var noExamsContract = NewContract(noExams.Id, canCreateExams: false, canUsePlacement: false);
            db.PartnerSubscriptions.AddRange(contract, otherContract, noExamsContract);
            db.CourseCurriculums.Add(new CourseCurriculum { CourseId = course.Id, CurriculumId = curriculum.Id });
            var section = new Section { Title = $"{Marker} محور", CurriculumId = curriculum.Id };
            db.Sections.Add(section);
            await db.SaveChangesAsync();

            db.PartnerSubscriptionPeriods.AddRange(NewPeriod(contract.Id), NewPeriod(otherContract.Id), NewPeriod(noExamsContract.Id));
            db.PartnerSubscriptionCourses.AddRange(
                new PartnerSubscriptionCourse { PartnerSubscriptionId = contract.Id, CourseId = course.Id, CanUsePlatformQuestionBank = true },
                new PartnerSubscriptionCourse { PartnerSubscriptionId = otherContract.Id, CourseId = course.Id, CanUsePlatformQuestionBank = true },
                new PartnerSubscriptionCourse { PartnerSubscriptionId = noExamsContract.Id, CourseId = course.Id, CanUsePlatformQuestionBank = true });

            var lesson = new Lesson { Title = $"{Marker} درس", Content = "-", SectionId = section.Id, UnitId = unit.Id };
            db.Lessons.Add(lesson);
            await db.SaveChangesAsync();

            Question NewQuestion(string suffix) => new()
            {
                Id = Guid.NewGuid(),
                Title = $"{Marker} نص {suffix}",
                ReferenceNumber = $"{Marker}{suffix}",
                CorrectAnswer = "أ",
                CurriculumId = curriculum.Id,
                SectionId = section.Id,
                LessonId = lesson.Id,
                IsReviewed = true,
                IsComplete = true,
                Options = new List<QuestionOption> { new() { Text = "أ" }, new() { Text = "ب" } }
            };

            var q1 = NewQuestion("سؤال1");
            var q2 = NewQuestion("سؤال2");
            var substitute = NewQuestion("بديل");
            db.Questions.AddRange(q1, q2, substitute);

            var model = NewModel(ModelTitle, ProfessionalModelType.Exam, curriculum.Id, isGlobal: true);
            var homeworkModel = NewModel(HomeworkModelTitle, ProfessionalModelType.Homework, curriculum.Id, isGlobal: true);
            var foreignModel = NewModel(ForeignModelTitle, ProfessionalModelType.Exam, curriculum.Id, isGlobal: false);
            db.ProfessionalModels.AddRange(model, homeworkModel, foreignModel);

            var batch = NewBatch("دفعة", branch.Id, course.Id);
            var foreignBatch = NewBatch("دفعة شريك آخر", otherBranch.Id, course.Id);
            var noExamsBatch = NewBatch("دفعة بلا اختبارات", noExamsBranch.Id, course.Id);
            db.Batches.AddRange(batch, foreignBatch, noExamsBatch);

            var student = NewStudent("1", StudentUser, branch, active: true);
            var inactive = NewStudent("2", null, branch, active: false);
            var foreignStudent = NewStudent("3", null, otherBranch, active: true);
            db.Students.AddRange(student, inactive, foreignStudent);
            await db.SaveChangesAsync();

            db.ProfessionalModelPartners.Add(new ProfessionalModelPartner { ProfessionalModelId = foreignModel.Id, PartnerId = other.Id });
            db.ProfessionalModelQuestions.AddRange(
                new ProfessionalModelQuestion { ModelId = model.Id, QuestionId = q1.Id, OrderNumber = 1 },
                new ProfessionalModelQuestion { ModelId = model.Id, QuestionId = q2.Id, OrderNumber = 2 },
                new ProfessionalModelQuestion { ModelId = homeworkModel.Id, QuestionId = q1.Id, OrderNumber = 1 },
                new ProfessionalModelQuestion { ModelId = foreignModel.Id, QuestionId = q1.Id, OrderNumber = 1 });
            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = student.StudentID, BatchId = batch.Id },
                new StudentBatchEnrollment { StudentID = inactive.StudentID, BatchId = batch.Id },
                new StudentBatchEnrollment { StudentID = foreignStudent.StudentID, BatchId = foreignBatch.Id },
                new StudentBatchEnrollment { StudentID = foreignStudent.StudentID, BatchId = noExamsBatch.Id });
            await db.SaveChangesAsync();

            _partnerId = partner.Id;
            _otherPartnerId = other.Id;
            _noExamsPartnerId = noExams.Id;
            _batchId = batch.Id;
            _foreignBatchId = foreignBatch.Id;
            _modelId = model.Id;
            _homeworkModelId = homeworkModel.Id;
            _foreignModelId = foreignModel.Id;
            _studentId = student.StudentID;
            _q1 = q1.Id;
            _substitute = substitute.Id;
        }

        public Task DisposeAsync() => CleanupAsync();

        private static Partner NewPartner(string suffix) => new()
        {
            Name = $"{Marker} شريك {suffix}",
            Code = $"{Marker}{suffix}",
            PartnershipStart = DateTime.Today.AddDays(-1),
            PartnershipEnd = DateTime.Today.AddDays(30)
        };

        private static Branch NewBranch(string name, Partner partner)
            => new() { Name = $"{Marker} {name}", Partner = partner, Location = "-", City = "-", State = "-", Country = "-" };

        private static Batch NewBatch(string name, int branchId, int courseId)
            => new() { Name = $"{Marker} {name}", BranchId = branchId, CourseId = courseId, StartDate = DateTime.UtcNow };

        private static PartnerSubscription NewContract(int partnerId, bool canCreateExams, bool canUsePlacement) => new()
        {
            PartnerId = partnerId,
            StartDate = DateTime.Today.AddDays(-1),
            EndDate = DateTime.Today.AddDays(30),
            CanUseProfessionalModels = true,
            CanCreateHomework = true,
            CanCreateExams = canCreateExams,
            CanUsePlacementExams = canUsePlacement
        };

        private static PartnerSubscriptionPeriod NewPeriod(int subscriptionId) => new()
        {
            PartnerSubscriptionId = subscriptionId,
            StartDate = DateTime.Today.AddDays(-1),
            EndDate = DateTime.Today.AddDays(30)
        };

        private static ProfessionalModel NewModel(string title, ProfessionalModelType type, int curriculumId, bool isGlobal) => new()
        {
            Title = title,
            Description = "-",
            CreatedBy = "-",
            CreatedAt = DateTime.UtcNow,
            IsForPartners = true,
            IsGlobal = isGlobal,
            ModelType = type,
            CurriculumId = curriculumId
        };

        private static ApplicationUser NewUser(string id) => new()
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant(),
            Email = $"{id}@test.local",
            NormalizedEmail = $"{id}@TEST.LOCAL",
            FullName = id,
            SecurityStamp = Guid.NewGuid().ToString(),
            IsActive = true
        };

        private static Student NewStudent(string suffix, string? userId, Branch branch, bool active) => new()
        {
            NationalID = $"{Marker}{suffix}",
            FullName = $"{Marker} طالب {suffix}",
            Gender = "ذكر",
            Age = 17,
            School = "-",
            Level = "-",
            UserId = userId,
            Branch = branch,
            IsActiveForLearning = active
        };

        private async Task CleanupAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPMS5H%')";
            const string batches = "(SELECT Id FROM Batches WHERE Name LIKE 'PPMS5H%')";
            const string assignments = "(SELECT Id FROM ExamAssignmentsToBatches WHERE BatchId IN " + batches + ")";
            const string models = "(SELECT Id FROM ProfessionalModels WHERE Title LIKE 'PPMS5H%')";
            const string partners = "(SELECT Id FROM Partners WHERE Code LIKE 'PPMS5H%')";
            const string subscriptions = "(SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN " + partners + ")";

            foreach (var sql in new[]
            {
                $"DELETE FROM Notifications WHERE StudentID IN {students}",
                "DELETE FROM SystemLogs WHERE Description LIKE N'%PPMS5H%'",
                $"DELETE FROM QuestionAttemptNew WHERE StudentId IN {students}",
                $"DELETE FROM ExamStudentQuestionOrders WHERE ExamAssignmentId IN {assignments}",
                $"DELETE FROM ExamStudentStatuses WHERE StudentId IN {students}",
                $"DELETE FROM ExamAssignments WHERE StudentId IN {students}",
                $"DELETE FROM ExamQuestions WHERE ExamAssignmentId IN {assignments}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE BatchId IN {batches}",
                $"DELETE FROM Exams WHERE SourceProfessionalModelId IN {models} OR OwnerPartnerId IN {partners}",
                $"DELETE FROM ProfessionalModelPartners WHERE ProfessionalModelId IN {models}",
                $"DELETE FROM ProfessionalModelQuestions WHERE ModelId IN {models}",
                $"DELETE FROM ProfessionalModels WHERE Id IN {models}",
                "DELETE FROM QuestionOptions WHERE QuestionId IN (SELECT Id FROM Questions WHERE ReferenceNumber LIKE 'PPMS5H%')",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPMS5H%'",
                "DELETE FROM Lessons WHERE Title LIKE 'PPMS5H%'",
                "DELETE FROM Units WHERE Title LIKE 'PPMS5H%'",
                "DELETE FROM Sections WHERE Title LIKE 'PPMS5H%'",
                "DELETE FROM CourseCurriculums WHERE CurriculumId IN (SELECT Id FROM Curriculums WHERE Title LIKE 'PPMS5H%')",
                "DELETE FROM Curriculums WHERE Title LIKE 'PPMS5H%'",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {batches}",
                "DELETE FROM Batches WHERE Name LIKE 'PPMS5H%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPMS5H%'",
                $"DELETE FROM PartnerSubscriptionPeriods WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptionCourses WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptions WHERE Id IN {subscriptions}",
                $"DELETE FROM UserPartners WHERE PartnerId IN {partners}",
                "DELETE FROM Branches WHERE Name LIKE 'PPMS5H%'",
                "DELETE FROM Partners WHERE Code LIKE 'PPMS5H%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPMS5H%'",
                "DELETE FROM AspNetUsers WHERE Id LIKE 'ppm-s5-%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        // =====================================================
        // مساعدات HTTP
        // =====================================================
        private async Task<HttpClient> PartnerClientAsync(string userId, int partnerId)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Partner");

            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(userId);
            var select = new HttpRequestMessage(HttpMethod.Post, "/Partner/PartnerContext/Set")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["partnerId"] = partnerId.ToString() })
            };
            select.Headers.Add("Cookie", cookie);
            select.Headers.Add("RequestVerificationToken", token);

            var response = await client.SendAsync(select);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var sessionCookie = string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
            client.DefaultRequestHeaders.Add("Cookie", sessionCookie);
            return client;
        }

        private Task<HttpClient> OwnPartnerAsync() => PartnerClientAsync(PartnerUser, _partnerId);

        private async Task<HttpResponseMessage> PostAsync(HttpClient client, string userId, string path, IEnumerable<KeyValuePair<string, string>> fields)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(userId);
            var session = client.DefaultRequestHeaders.GetValues("Cookie").Single();
            client.DefaultRequestHeaders.Remove("Cookie");

            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("Cookie", $"{session}; {cookie}");
            request.Headers.Add("RequestVerificationToken", token);

            try
            {
                return await client.SendAsync(request);
            }
            finally
            {
                client.DefaultRequestHeaders.Add("Cookie", session);
            }
        }

        private static KeyValuePair<string, string> Field(string name, object value) => new(name, value.ToString()!);

        private static string SaudiLocal(int days)
            => DateTime.UtcNow.AddHours(3).AddDays(days).ToString("yyyy-MM-ddTHH:mm");

        private List<KeyValuePair<string, string>> Form(string intent, params int[] batchIds)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                Field("ModelId", _modelId),
                Field("Intent", intent),
                Field("StartAtLocal", SaudiLocal(0)),
                Field("EndAtLocal", SaudiLocal(5)),
                Field("DurationMinutes", 40)
            };
            fields.AddRange(batchIds.Select(id => Field("BatchIds", id)));
            return fields;
        }

        private Task<HttpResponseMessage> SendAsync(HttpClient client, IEnumerable<KeyValuePair<string, string>> fields)
            => PostAsync(client, PartnerUser, $"{Base}/SendExam", fields);

        private static async Task<string> HtmlOf(HttpResponseMessage response)
            => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()); // Razor يرمّز العربية

        private async Task<T> ReadAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private Task<int> ExamCountAsync(int? modelId = null)
        {
            var id = modelId ?? _modelId;
            return ReadAsync(db => db.Exams.CountAsync(e => e.SourceProfessionalModelId == id));
        }

        private async Task<int> SendExamAsync()
        {
            Assert.Equal(HttpStatusCode.Redirect, (await SendAsync(await OwnPartnerAsync(), Form(SendFromModelVm.IntentSend, _batchId))).StatusCode);
            return await ReadAsync(db => db.ExamAssignmentsToBatches.Where(a => a.Exam.SourceProfessionalModelId == _modelId).Select(a => a.Id).SingleAsync());
        }

        // =====================================================
        // قائمة نماذج الاختبار
        // =====================================================
        [Fact]
        public async Task Exams_ListsOnlyExamModelsAvailableToPartner_WithSendAndPlacementLinks()
        {
            var client = await OwnPartnerAsync();
            var response = await client.GetAsync($"{Base}/Exams");
            var html = await HtmlOf(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(ModelTitle, html);
            Assert.DoesNotContain(HomeworkModelTitle, html);
            Assert.DoesNotContain(ForeignModelTitle, html);
            Assert.Contains($"{Base}/SendExam/{_modelId}", html);
            Assert.Contains($"/Partner/PlacementExams/CreateFromProfessionalModel?modelId={_modelId}", html);

            // صفحة الواجبات لا تعرض نماذج الاختبار
            Assert.DoesNotContain(ModelTitle, await HtmlOf(await client.GetAsync(Base)));
        }

        [Fact]
        public async Task Exams_ContractWithoutPlacement_HidesPlacementLink_AndSeesOnlyItsModels()
        {
            var html = await HtmlOf(await (await PartnerClientAsync(OtherPartnerUser, _otherPartnerId)).GetAsync($"{Base}/Exams"));

            Assert.Contains(ModelTitle, html);
            Assert.Contains(ForeignModelTitle, html);
            Assert.DoesNotContain("CreateFromProfessionalModel", html);
        }

        [Fact]
        public async Task Sidebar_LinksToExamModels()
        {
            var html = await HtmlOf(await (await OwnPartnerAsync()).GetAsync($"{Base}/Exams"));

            Assert.Contains("اختبار من نموذج", html);
        }

        [Fact]
        public async Task PlacementLink_PreselectsAvailableModel_AndIgnoresOthers()
        {
            var client = await OwnPartnerAsync();

            var html = await HtmlOf(await client.GetAsync($"/Partner/PlacementExams/CreateFromProfessionalModel?modelId={_modelId}"));
            Assert.Contains($"selected=\"selected\" value=\"{_modelId}\"", html);

            var foreign = await HtmlOf(await client.GetAsync($"/Partner/PlacementExams/CreateFromProfessionalModel?modelId={_foreignModelId}"));
            Assert.DoesNotContain(ForeignModelTitle, foreign);
            Assert.DoesNotContain("selected=\"selected\" value=", foreign);
        }

        // =====================================================
        // صفحة الإرسال
        // =====================================================
        [Fact]
        public async Task SendExam_Get_RendersForm_ForAvailableExamModelOnly()
        {
            var client = await OwnPartnerAsync();

            var page = await client.GetAsync($"{Base}/SendExam/{_modelId}");
            var html = await HtmlOf(page);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains("إرسال اختبار من نموذج", html);
            Assert.Contains("name=\"DurationMinutes\"", html);
            Assert.Contains($"{Marker} دفعة", html);
            Assert.DoesNotContain($"{Marker} دفعة شريك آخر", html);
            Assert.Contains($"{Base}/ExamReplacementCandidates/{_modelId}", html);

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/SendExam/{_homeworkModelId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/SendExam/{_foreignModelId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/SendHomework/{_modelId}")).StatusCode);
        }

        [Fact]
        public async Task SendHomework_Get_StillRendersWithoutDuration()
        {
            var html = await HtmlOf(await (await OwnPartnerAsync()).GetAsync($"{Base}/SendHomework/{_homeworkModelId}"));

            Assert.Contains("إرسال واجب من نموذج", html);
            Assert.DoesNotContain("name=\"DurationMinutes\"", html);
        }

        [Fact]
        public async Task SendExam_Preview_WritesNothing_ThenSend_WritesAndRedirectsToSentExams()
        {
            var client = await OwnPartnerAsync();

            var preview = await SendAsync(client, Form(SendFromModelVm.IntentPreview, _batchId));
            var html = await HtmlOf(preview);
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            Assert.Contains("مراجعة الإرسال", html);
            Assert.Contains("إرسال الاختبار", html);
            Assert.Equal(0, await ExamCountAsync());

            var sent = await SendAsync(client, Form(SendFromModelVm.IntentSend, _batchId));
            Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
            Assert.Equal("/Partner/PartnerExam/SentExams", sent.Headers.Location!.OriginalString);

            var exam = await ReadAsync(db => db.Exams.AsNoTracking().SingleAsync(e => e.SourceProfessionalModelId == _modelId));
            Assert.Equal(_partnerId, exam.OwnerPartnerId);
            Assert.Equal(ExamType.Manual, exam.Type);
            Assert.Equal(40, exam.DurationMinutes);

            // غير النشط لا يستلم
            Assert.Equal(_studentId, await ReadAsync(db => db.ExamStudentStatuses.Where(s => s.ExamId == exam.Id).Select(s => s.StudentId).SingleAsync()));
        }

        [Fact]
        public async Task SendExam_ValidationErrors_RerenderWithoutWriting()
        {
            var client = await OwnPartnerAsync();

            var noBatch = await HtmlOf(await SendAsync(client, Form(SendFromModelVm.IntentPreview)));
            Assert.Contains("اختر دفعة واحدة على الأقل", noBatch);

            var fields = Form(SendFromModelVm.IntentSend, _batchId);
            fields.RemoveAll(f => f.Key == "DurationMinutes");
            fields.Add(Field("DurationMinutes", 1));
            var badDuration = await SendAsync(client, fields);
            Assert.Equal(HttpStatusCode.OK, badDuration.StatusCode);
            Assert.Contains("مدة الاختبار يجب أن تكون بين", await HtmlOf(badDuration));

            var noDates = Form(SendFromModelVm.IntentSend, _batchId);
            noDates.RemoveAll(f => f.Key == "StartAtLocal");
            Assert.Contains("حدد موعد بداية الاختبار", await HtmlOf(await SendAsync(client, noDates)));

            Assert.Equal(0, await ExamCountAsync());
        }

        // =====================================================
        // العزل والعقد
        // =====================================================
        [Fact]
        public async Task OtherPartnersBatchAndModel_AreRejected_WithoutWriting()
        {
            var client = await OwnPartnerAsync();

            var foreignBatch = await SendAsync(client, Form(SendFromModelVm.IntentSend, _foreignBatchId));
            Assert.Equal(HttpStatusCode.OK, foreignBatch.StatusCode);
            Assert.Contains("دفعات غير صالحة", await HtmlOf(foreignBatch));
            Assert.Equal(0, await ExamCountAsync());

            var foreignModel = Form(SendFromModelVm.IntentSend, _batchId);
            foreignModel[0] = Field("ModelId", _foreignModelId);
            Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, foreignModel)).StatusCode);
            Assert.Equal(0, await ExamCountAsync(_foreignModelId));

            // نموذج الواجب لا يُرسل كاختبار بطلب مصطنع
            var homework = Form(SendFromModelVm.IntentSend, _batchId);
            homework[0] = Field("ModelId", _homeworkModelId);
            Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, homework)).StatusCode);
            Assert.Equal(0, await ExamCountAsync(_homeworkModelId));
        }

        [Fact]
        public async Task ContractWithoutExams_CannotUseExamSendPages_ButSeesContractMessage()
        {
            var client = await PartnerClientAsync(NoExamsUser, _noExamsPartnerId);

            var get = await client.GetAsync($"{Base}/SendExam/{_modelId}");
            Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
            Assert.Contains("هذه الخدمة غير مشمولة", await HtmlOf(get));

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Base}/ExamReplacementCandidates/{_modelId}?questionId={_q1}")).StatusCode);

            var post = await PostAsync(client, NoExamsUser, $"{Base}/SendExam", Form(SendFromModelVm.IntentSend, _batchId));
            Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
            Assert.Equal(0, await ExamCountAsync());

            var index = await HtmlOf(await client.GetAsync($"{Base}/Exams"));
            Assert.Contains("إرسال الاختبارات غير مشمول في عقدكم", index);
            Assert.DoesNotContain(ModelTitle, index);
        }

        [Fact]
        public async Task PostWithoutAntiForgery_IsRejected()
        {
            var client = await OwnPartnerAsync();

            var response = await client.PostAsync($"{Base}/SendExam", new FormUrlEncodedContent(Form(SendFromModelVm.IntentSend, _batchId)));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, await ExamCountAsync());
        }

        [Fact]
        public async Task ExamReplacementCandidates_ReturnBankQuestions_AndHideOtherPartnersModel()
        {
            var client = await OwnPartnerAsync();

            var html = await HtmlOf(await client.GetAsync($"{Base}/ExamReplacementCandidates/{_modelId}?questionId={_q1}"));
            Assert.Contains($"{Marker} نص بديل", html);

            var foreign = await HtmlOf(await client.GetAsync($"{Base}/ExamReplacementCandidates/{_foreignModelId}?questionId={_q1}"));
            Assert.DoesNotContain($"{Marker} نص بديل", foreign);
        }

        [Fact]
        public async Task SendExam_WithReplacement_SendsTheSubstitute_AndWarnsOnRepeat()
        {
            var client = await OwnPartnerAsync();
            var fields = Form(SendFromModelVm.IntentSend, _batchId);
            fields.Add(Field($"Replacements[{_q1}]", _substitute));

            Assert.Equal(HttpStatusCode.Redirect, (await SendAsync(client, fields)).StatusCode);

            var sent = await ReadAsync(db => db.ExamQuestions.Where(q => q.Exam!.SourceProfessionalModelId == _modelId).OrderBy(q => q.Order).Select(q => q.QuestionId).FirstAsync());
            Assert.Equal(_substitute, sent);

            // الإرسال الثاني: تأكيد إلزامي
            var unconfirmed = await SendAsync(client, Form(SendFromModelVm.IntentSend, _batchId));
            var html = await HtmlOf(unconfirmed);
            Assert.Equal(HttpStatusCode.OK, unconfirmed.StatusCode);
            Assert.Contains("id=\"confirmWarnings\"", html);
            Assert.Equal(1, await ExamCountAsync());
        }

        // =====================================================
        // الظهور: الشريك والطالب والأدمن (PPM-S5.3)
        // =====================================================
        [Fact]
        public async Task SentExams_ShowsModelTag_ForOwnerOnly()
        {
            await SendExamAsync();

            var own = await HtmlOf(await (await OwnPartnerAsync()).GetAsync("/Partner/PartnerExam/SentExams"));
            Assert.Contains($"اختبار من النموذج {ModelTitle}", own);
            Assert.Contains($"من نموذج: {ModelTitle}", own);

            var other = await HtmlOf(await (await PartnerClientAsync(OtherPartnerUser, _otherPartnerId)).GetAsync("/Partner/PartnerExam/SentExams"));
            Assert.DoesNotContain(ModelTitle, other);
        }

        [Fact]
        public async Task SentExams_CountsRecipientsOnly_AndStudentsPageWorks()
        {
            var assignmentId = await SendExamAsync();
            var client = await OwnPartnerAsync();

            // الدفعة فيها طالبان، والمستلم طالب واحد نشط
            var rows = await (await client.GetAsync("/Partner/PartnerExam/SentExams")).Content.ReadAsStringAsync();
            Assert.Contains("<td class=\"text-danger fw-bold\">1</td>", rows);

            var students = await client.GetAsync($"/Partner/PartnerExam/Students?examAssignmentId={assignmentId}");
            Assert.Equal(HttpStatusCode.OK, students.StatusCode);
            Assert.Contains($"{Marker} طالب 1", await HtmlOf(students));
        }

        [Fact]
        public async Task PartnerPlacementList_DoesNotShowModelExam()
        {
            await SendExamAsync();

            var html = await HtmlOf(await (await OwnPartnerAsync()).GetAsync("/Partner/PlacementExams"));

            Assert.DoesNotContain($"{Marker} دفعة", html);
        }

        private HttpClient StudentClient()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, StudentUser);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Student");
            return client;
        }

        [Fact]
        public async Task SentExam_AppearsForStudent_AndOpens()
        {
            var assignmentId = await SendExamAsync();
            var student = StudentClient();

            foreach (var page in new[] { "AllExams", "RequiredExams" })
            {
                var list = await student.GetAsync($"/Students/StudentExamPages/{page}");
                Assert.Equal(HttpStatusCode.OK, list.StatusCode);
                Assert.Contains($"اختبار من النموذج {ModelTitle}", await HtmlOf(list));
            }

            // التخزين UTC، والطالب يرى الموعد الذي حدده الشريك بتوقيت السعودية
            var storedEnd = await ReadAsync(db => db.ExamAssignmentsToBatches.Where(a => a.Id == assignmentId).Select(a => a.EndAt!.Value).SingleAsync());
            var required = await HtmlOf(await student.GetAsync("/Students/StudentExamPages/RequiredExams"));
            Assert.Contains(storedEnd.AddHours(3).ToString("yyyy/MM/dd HH:mm"), required);
            Assert.DoesNotContain(storedEnd.ToString("yyyy/MM/dd HH:mm"), required);

            var start = await student.GetAsync($"/Students/StudentExamFlow/StartExam?examAssignmentId={assignmentId}");
            Assert.Equal(HttpStatusCode.OK, start.StatusCode);

            var status = await ReadAsync(db => db.ExamStudentStatuses.AsNoTracking().SingleAsync(s => s.ExamAssignmentId == assignmentId && s.StudentId == _studentId));
            Assert.NotNull(status.StartedAt);
            Assert.Equal(status.StartedAt!.Value.AddMinutes(40), status.EndAt);
        }

        // بعد دخول الطالب تُنشأ له صفوف ترتيب الأسئلة، وكان قيدها يمنع الحذف برسالة SQL خام
        [Fact]
        public async Task DeleteSentExam_AfterStudentStarted_RemovesItAndClearsTheRepeatWarning()
        {
            var assignmentId = await SendExamAsync();
            Assert.Equal(HttpStatusCode.OK, (await StudentClient().GetAsync($"/Students/StudentExamFlow/StartExam?examAssignmentId={assignmentId}")).StatusCode);
            Assert.True(await ReadAsync(db => db.ExamStudentQuestionOrders.AnyAsync(o => o.ExamAssignmentId == assignmentId)));

            var client = await OwnPartnerAsync();
            var response = await PostAsync(client, PartnerUser, "/Partner/PartnerExam/DeleteSentExam",
                new[] { Field("examAssignmentId", assignmentId), Field("forceDelete", "true") });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.False(await ReadAsync(db => db.ExamAssignmentsToBatches.AnyAsync(a => a.Id == assignmentId)));
            Assert.False(await ReadAsync(db => db.ExamStudentQuestionOrders.AnyAsync(o => o.ExamAssignmentId == assignmentId)));

            var preview = await HtmlOf(await SendAsync(client, Form(SendFromModelVm.IntentPreview, _batchId)));
            Assert.DoesNotContain("أُرسل هذا النموذج سابقًا", preview);
        }

        private HttpClient OwnerClient()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, OwnerUser);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Owner");
            return client;
        }

        private async Task<HttpResponseMessage> OwnerPostAsync(string path, IEnumerable<KeyValuePair<string, string>> fields)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(OwnerUser);
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("Cookie", cookie);
            request.Headers.Add("RequestVerificationToken", token);
            return await OwnerClient().SendAsync(request);
        }

        [Fact]
        public async Task SentExam_IsHiddenFromAdminLists_AndBulkArchive()
        {
            var assignmentId = await SendExamAsync();
            var title = $"اختبار من النموذج {ModelTitle}";
            var admin = OwnerClient();

            var index = await admin.GetAsync("/Admin/ExamAssignments");
            Assert.Equal(HttpStatusCode.OK, index.StatusCode);
            Assert.DoesNotContain(title, await HtmlOf(index));

            var data = await OwnerPostAsync($"/Admin/ExamAssignments/LoadExamAssignmentsData?batchId={_batchId}", Array.Empty<KeyValuePair<string, string>>());
            Assert.Equal(HttpStatusCode.OK, data.StatusCode);
            Assert.DoesNotContain("\"id\"", await data.Content.ReadAsStringAsync());

            var modelAssignments = await admin.GetAsync("/Admin/AdminLessonCompletions/ModelAssignments");
            Assert.Equal(HttpStatusCode.OK, modelAssignments.StatusCode);
            Assert.DoesNotContain(title, await HtmlOf(modelAssignments));

            var archive = await OwnerPostAsync("/Admin/ExamAssignments/ArchiveExamAssignmentsByBatches", new[] { Field("selectedBatchIds", _batchId) });
            Assert.Equal(HttpStatusCode.Redirect, archive.StatusCode);
            Assert.False(await ReadAsync(db => db.ExamAssignmentsToBatches.Where(a => a.Id == assignmentId).Select(a => a.IsArchived).SingleAsync()));

            var single = await OwnerPostAsync("/Admin/AdminLessonCompletions/ArchiveModelExam", new[] { Field("id", assignmentId) });
            Assert.Equal(HttpStatusCode.Redirect, single.StatusCode);
            Assert.False(await ReadAsync(db => db.ExamAssignmentsToBatches.Where(a => a.Id == assignmentId).Select(a => a.IsArchived).SingleAsync()));
        }
    }
}
