using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.PartnerProfessionalModels
{
    // PPM-S4 — إرسال واجب من نموذج عبر HTTP حقيقي: الشاشات والعزل والعقد والتكرار، ثم ظهور الواجب عند الطالب.
    [Collection("SharedIntegrationDb")]
    public class PartnerModelSendIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Marker = "PPMS4";
        private const string PartnerUser = "ppm-s4-partner";
        private const string OtherPartnerUser = "ppm-s4-partner-b";
        private const string NoHomeworkUser = "ppm-s4-partner-c";
        private const string StudentUser = "ppm-s4-student";
        private const string InactiveUser = "ppm-s4-inactive";
        private const string Base = "/Partner/ProfessionalModels";
        private const string ModelTitle = "PPMS4 نموذج واجب";

        private readonly CustomWebApplicationFactory _factory;
        private int _partnerId;
        private int _otherPartnerId;
        private int _noHomeworkPartnerId;
        private int _batchId;
        private int _secondBatchId;
        private int _emptyBatchId;
        private int _foreignBatchId;
        private int _modelId;
        private int _examModelId;
        private int _foreignModelId;
        private int _studentId;
        private int _inactiveId;
        private Guid _q1;
        private Guid _q2;
        private Guid _q3;
        private Guid _substitute;
        private Guid _otherLessonQuestion;

        public PartnerModelSendIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var today = DateTime.Today;

            var partner = NewPartner("A");
            var other = NewPartner("B");
            var noHomework = NewPartner("C");
            var branch = NewBranch("فرع", partner);
            var otherBranch = NewBranch("فرع آخر", other);
            var noHomeworkBranch = NewBranch("فرع بلا واجبات", noHomework);
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var curriculum = new Curriculum { Title = $"{Marker} منهج", Description = "-", CurriculumTypeName = "-" };
            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.AddRange(partner, other, noHomework, branch, otherBranch, noHomeworkBranch, course, curriculum, unit);
            db.Users.AddRange(NewUser(PartnerUser), NewUser(OtherPartnerUser), NewUser(NoHomeworkUser), NewUser(StudentUser), NewUser(InactiveUser));
            await db.SaveChangesAsync();

            db.UserPartners.AddRange(
                new UserPartner { UserId = PartnerUser, PartnerId = partner.Id },
                new UserPartner { UserId = OtherPartnerUser, PartnerId = other.Id },
                new UserPartner { UserId = NoHomeworkUser, PartnerId = noHomework.Id });

            var contract = NewContract(partner.Id, canCreateHomework: true);
            var otherContract = NewContract(other.Id, canCreateHomework: true);
            var noHomeworkContract = NewContract(noHomework.Id, canCreateHomework: false);
            db.PartnerSubscriptions.AddRange(contract, otherContract, noHomeworkContract);
            db.CourseCurriculums.Add(new CourseCurriculum { CourseId = course.Id, CurriculumId = curriculum.Id });
            var section = new Section { Title = $"{Marker} محور", CurriculumId = curriculum.Id };
            db.Sections.Add(section);
            await db.SaveChangesAsync();

            db.PartnerSubscriptionPeriods.AddRange(NewPeriod(contract.Id), NewPeriod(otherContract.Id), NewPeriod(noHomeworkContract.Id));
            db.PartnerSubscriptionCourses.AddRange(
                new PartnerSubscriptionCourse { PartnerSubscriptionId = contract.Id, CourseId = course.Id, CanUsePlatformQuestionBank = true },
                new PartnerSubscriptionCourse { PartnerSubscriptionId = otherContract.Id, CourseId = course.Id, CanUsePlatformQuestionBank = true },
                new PartnerSubscriptionCourse { PartnerSubscriptionId = noHomeworkContract.Id, CourseId = course.Id, CanUsePlatformQuestionBank = true });

            var lesson = new Lesson { Title = $"{Marker} درس", Content = "-", SectionId = section.Id, UnitId = unit.Id };
            var otherLesson = new Lesson { Title = $"{Marker} درس آخر", Content = "-", SectionId = section.Id, UnitId = unit.Id };
            db.Lessons.AddRange(lesson, otherLesson);
            await db.SaveChangesAsync();

            Question NewQuestion(string suffix, int lessonId) => new()
            {
                Id = Guid.NewGuid(),
                Title = $"{Marker} نص {suffix}",
                ReferenceNumber = $"{Marker}{suffix}",
                CorrectAnswer = "أ",
                CurriculumId = curriculum.Id,
                SectionId = section.Id,
                LessonId = lessonId,
                IsReviewed = true,
                IsComplete = true,
                Options = new List<QuestionOption> { new() { Text = "أ" }, new() { Text = "ب" } }
            };

            var q1 = NewQuestion("سؤال1", lesson.Id);
            var q2 = NewQuestion("سؤال2", lesson.Id);
            var q3 = NewQuestion("سؤال3", lesson.Id);
            var substitute = NewQuestion("بديل", lesson.Id);
            var otherLessonQuestion = NewQuestion("درس-آخر", otherLesson.Id);
            db.Questions.AddRange(q1, q2, q3, substitute, otherLessonQuestion);

            var model = NewModel(ModelTitle, ProfessionalModelType.Homework, curriculum.Id, isGlobal: true);
            var examModel = NewModel($"{Marker} نموذج اختبار", ProfessionalModelType.Exam, curriculum.Id, isGlobal: true);
            var foreignModel = NewModel($"{Marker} نموذج شريك آخر", ProfessionalModelType.Homework, curriculum.Id, isGlobal: false);
            db.ProfessionalModels.AddRange(model, examModel, foreignModel);

            var batch = NewBatch("دفعة", branch.Id, course.Id);
            var secondBatch = NewBatch("دفعة ثانية", branch.Id, course.Id);
            var emptyBatch = NewBatch("دفعة فارغة", branch.Id, course.Id);
            var foreignBatch = NewBatch("دفعة شريك آخر", otherBranch.Id, course.Id);
            var noHomeworkBatch = NewBatch("دفعة بلا واجبات", noHomeworkBranch.Id, course.Id);
            db.Batches.AddRange(batch, secondBatch, emptyBatch, foreignBatch, noHomeworkBatch);

            var student = NewStudent("1", StudentUser, branch, active: true);
            var inactive = NewStudent("2", InactiveUser, branch, active: false);
            var foreignStudent = NewStudent("3", null, otherBranch, active: true);
            var secondStudent = NewStudent("4", null, branch, active: true);
            db.Students.AddRange(student, inactive, foreignStudent, secondStudent);
            await db.SaveChangesAsync();

            db.ProfessionalModelPartners.Add(new ProfessionalModelPartner { ProfessionalModelId = foreignModel.Id, PartnerId = other.Id });
            db.ProfessionalModelQuestions.AddRange(
                new ProfessionalModelQuestion { ModelId = model.Id, QuestionId = q1.Id, OrderNumber = 1 },
                new ProfessionalModelQuestion { ModelId = model.Id, QuestionId = q2.Id, OrderNumber = 2 },
                new ProfessionalModelQuestion { ModelId = model.Id, QuestionId = q3.Id, OrderNumber = 3 },
                new ProfessionalModelQuestion { ModelId = examModel.Id, QuestionId = q1.Id, OrderNumber = 1 },
                new ProfessionalModelQuestion { ModelId = foreignModel.Id, QuestionId = q1.Id, OrderNumber = 1 });
            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = student.StudentID, BatchId = batch.Id },
                new StudentBatchEnrollment { StudentID = inactive.StudentID, BatchId = batch.Id },
                new StudentBatchEnrollment { StudentID = secondStudent.StudentID, BatchId = secondBatch.Id },
                new StudentBatchEnrollment { StudentID = foreignStudent.StudentID, BatchId = foreignBatch.Id },
                new StudentBatchEnrollment { StudentID = foreignStudent.StudentID, BatchId = noHomeworkBatch.Id });
            await db.SaveChangesAsync();

            _partnerId = partner.Id;
            _otherPartnerId = other.Id;
            _noHomeworkPartnerId = noHomework.Id;
            _batchId = batch.Id;
            _secondBatchId = secondBatch.Id;
            _emptyBatchId = emptyBatch.Id;
            _foreignBatchId = foreignBatch.Id;
            _modelId = model.Id;
            _examModelId = examModel.Id;
            _foreignModelId = foreignModel.Id;
            _studentId = student.StudentID;
            _inactiveId = inactive.StudentID;
            _q1 = q1.Id;
            _q2 = q2.Id;
            _q3 = q3.Id;
            _substitute = substitute.Id;
            _otherLessonQuestion = otherLessonQuestion.Id;
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

        private static PartnerSubscription NewContract(int partnerId, bool canCreateHomework) => new()
        {
            PartnerId = partnerId,
            StartDate = DateTime.Today.AddDays(-1),
            EndDate = DateTime.Today.AddDays(30),
            CanUseProfessionalModels = true,
            CanCreateHomework = canCreateHomework
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

            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPMS4%')";
            const string batches = "(SELECT Id FROM Batches WHERE Name LIKE 'PPMS4%')";
            const string sets = "(SELECT Id FROM HomeworkSets WHERE BatchId IN " + batches + ")";
            const string models = "(SELECT Id FROM ProfessionalModels WHERE Title LIKE 'PPMS4%')";
            const string partners = "(SELECT Id FROM Partners WHERE Code LIKE 'PPMS4%')";
            const string subscriptions = "(SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN " + partners + ")";

            foreach (var sql in new[]
            {
                $"DELETE FROM Notifications WHERE StudentID IN {students}",
                "DELETE FROM SystemLogs WHERE Description LIKE N'%PPMS4%'",
                $"DELETE FROM QuestionAttemptNew WHERE StudentId IN {students}",
                $"DELETE FROM Homeworks WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSetStudents WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSets WHERE BatchId IN {batches}",
                $"DELETE FROM ProfessionalModelPartners WHERE ProfessionalModelId IN {models}",
                $"DELETE FROM ProfessionalModelQuestions WHERE ModelId IN {models}",
                $"DELETE FROM ProfessionalModels WHERE Id IN {models}",
                "DELETE FROM QuestionOptions WHERE QuestionId IN (SELECT Id FROM Questions WHERE ReferenceNumber LIKE 'PPMS4%')",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPMS4%'",
                "DELETE FROM Lessons WHERE Title LIKE 'PPMS4%'",
                "DELETE FROM Units WHERE Title LIKE 'PPMS4%'",
                "DELETE FROM Sections WHERE Title LIKE 'PPMS4%'",
                "DELETE FROM CourseCurriculums WHERE CurriculumId IN (SELECT Id FROM Curriculums WHERE Title LIKE 'PPMS4%')",
                "DELETE FROM Curriculums WHERE Title LIKE 'PPMS4%'",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {batches}",
                "DELETE FROM Batches WHERE Name LIKE 'PPMS4%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPMS4%'",
                $"DELETE FROM PartnerSubscriptionPeriods WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptionCourses WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptions WHERE Id IN {subscriptions}",
                $"DELETE FROM UserPartners WHERE PartnerId IN {partners}",
                "DELETE FROM Branches WHERE Name LIKE 'PPMS4%'",
                "DELETE FROM Partners WHERE Code LIKE 'PPMS4%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPMS4%'",
                "DELETE FROM AspNetUsers WHERE Id LIKE 'ppm-s4-%'"
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
                Field("EndAtLocal", SaudiLocal(5))
            };
            fields.AddRange(batchIds.Select(id => Field("BatchIds", id)));
            return fields;
        }

        private Task<HttpResponseMessage> SendAsync(HttpClient client, IEnumerable<KeyValuePair<string, string>> fields)
            => PostAsync(client, PartnerUser, $"{Base}/SendHomework", fields);

        private static async Task<string> HtmlOf(HttpResponseMessage response)
            => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()); // Razor يرمّز العربية

        private async Task<T> ReadAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private Task<int> SetCountAsync()
            => ReadAsync(db => db.HomeworkSets.CountAsync(h => h.SourceProfessionalModelId == _modelId));

        // =====================================================
        // Index
        // =====================================================
        [Fact]
        public async Task Index_ListsOnlyHomeworkModelsAvailableToPartner()
        {
            var html = await HtmlOf(await (await OwnPartnerAsync()).GetAsync(Base));

            Assert.Contains(ModelTitle, html);
            Assert.Contains($"{Base}/SendHomework/{_modelId}", html);
            Assert.DoesNotContain($"{Marker} نموذج اختبار", html);
            Assert.DoesNotContain($"{Marker} نموذج شريك آخر", html);
        }

        // =====================================================
        // SendHomework GET
        // =====================================================
        [Fact]
        public async Task SendHomeworkPage_ShowsEligibleBatchesAndQuestions()
        {
            var response = await (await OwnPartnerAsync()).GetAsync($"{Base}/SendHomework/{_modelId}");
            var html = await HtmlOf(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains($"{Marker} دفعة ثانية", html);
            Assert.Contains($"value=\"{_batchId}\"", html);
            Assert.DoesNotContain($"{Marker} دفعة شريك آخر", html);
            Assert.DoesNotContain($"value=\"{_foreignBatchId}\"", html);
            Assert.Contains($"{Marker} نص سؤال1", html);

            // دفعة بلا طلاب نشطين لا يمكن اختيارها
            Assert.Contains("لا يوجد طلاب نشطون", html);
            Assert.Matches($"value=\"{_emptyBatchId}\"[^>]*disabled", html);
        }

        [Fact]
        public async Task SendHomeworkPage_UnavailableOrExamModel_IsNotFound()
        {
            var client = await OwnPartnerAsync();

            foreach (var id in new[] { _foreignModelId, _examModelId, 0, int.MaxValue })
            {
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/SendHomework/{id}")).StatusCode);
            }

            var post = Form(SendModelIntent.Send, _batchId);
            post[0] = Field("ModelId", _foreignModelId);
            Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, post)).StatusCode);
            Assert.Equal(0, await ReadAsync(db => db.HomeworkSets.CountAsync(h => h.SourceProfessionalModelId == _foreignModelId)));
        }

        [Fact]
        public async Task ContractWithoutHomework_CannotUseSendPages()
        {
            var client = await PartnerClientAsync(NoHomeworkUser, _noHomeworkPartnerId);

            var get = await client.GetAsync($"{Base}/SendHomework/{_modelId}");
            Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
            Assert.Contains("هذه الخدمة غير مشمولة", await HtmlOf(get));

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Base}/ReplacementCandidates/{_modelId}?questionId={_q1}")).StatusCode);

            var post = await PostAsync(client, NoHomeworkUser, $"{Base}/SendHomework", Form(SendModelIntent.Send, _batchId));
            Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
            Assert.Equal(0, await SetCountAsync());

            var index = await client.GetAsync(Base);
            Assert.Equal(HttpStatusCode.OK, index.StatusCode);
            Assert.Contains("إرسال الواجبات غير مشمول في عقدكم", await HtmlOf(index));
        }

        // =====================================================
        // المعاينة
        // =====================================================
        [Fact]
        public async Task Preview_ShowsSummary_AndWritesNothing()
        {
            var response = await SendAsync(await OwnPartnerAsync(), Form(SendModelIntent.Preview, _batchId));
            var html = await HtmlOf(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("مراجعة الإرسال", html);
            Assert.Contains("id=\"sendButton\"", html);
            Assert.DoesNotContain("id=\"confirmWarnings\"", html);
            Assert.Equal(0, await SetCountAsync());
        }

        [Fact]
        public async Task Preview_WithInvalidInput_ShowsErrorInsteadOfReview()
        {
            var client = await OwnPartnerAsync();

            var noBatch = await HtmlOf(await SendAsync(client, Form(SendModelIntent.Preview)));
            Assert.Contains("اختر دفعة واحدة على الأقل", noBatch);
            Assert.DoesNotContain("id=\"sendButton\"", noBatch);

            var reversed = Form(SendModelIntent.Preview, _batchId);
            reversed[3] = Field("EndAtLocal", SaudiLocal(-1));
            var reversedHtml = await HtmlOf(await SendAsync(client, reversed));
            Assert.Contains("موعد النهاية يجب أن يكون بعد موعد البداية", reversedHtml);
            Assert.DoesNotContain("id=\"sendButton\"", reversedHtml);

            var foreign = await HtmlOf(await SendAsync(client, Form(SendModelIntent.Preview, _foreignBatchId)));
            Assert.Contains("دفعات غير صالحة", foreign);

            var noDates = Form(SendModelIntent.Preview, _batchId).Where(f => !f.Key.EndsWith("AtLocal")).ToList();
            Assert.Contains("حدد موعد بداية الواجب", await HtmlOf(await SendAsync(client, noDates)));

            Assert.Equal(0, await SetCountAsync());
        }

        [Fact]
        public async Task PageEncodesPostedTitle()
        {
            var fields = Form(SendModelIntent.Preview, _batchId);
            fields.Add(Field("Title", "<script>alert(1)</script>"));

            var raw = await (await SendAsync(await OwnPartnerAsync(), fields)).Content.ReadAsStringAsync();

            Assert.DoesNotContain("<script>alert(1)", raw);
        }

        // =====================================================
        // الإرسال
        // =====================================================
        [Fact]
        public async Task Send_ReachesActiveStudentsOfSelectedBatchOnly_AndAppearsInSentList()
        {
            var client = await OwnPartnerAsync();

            var response = await SendAsync(client, Form(SendModelIntent.Send, _batchId));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Partner/SentHomeworks", response.Headers.Location!.OriginalString);

            var set = await ReadAsync(db => db.HomeworkSets.AsNoTracking().SingleAsync(h => h.SourceProfessionalModelId == _modelId));
            Assert.Equal(_batchId, set.BatchId);
            Assert.Equal(PartnerUser, set.AssignedByUserId);
            Assert.True(set.IsFromProfessionalModel);
            Assert.True(set.IsSent);
            Assert.Null(set.LectureId);
            Assert.NotNull(set.StartAt);

            var recipients = await ReadAsync(db => db.HomeworkSetStudents.Where(s => s.HomeworkSetId == set.Id).Select(s => s.StudentId).ToListAsync());
            Assert.Equal(new[] { _studentId }, recipients);

            var questions = await ReadAsync(db => db.Homeworks.Where(h => h.HomeworkSetId == set.Id).Select(h => h.QuestionId).ToListAsync());
            Assert.Equal(new[] { _q1, _q2, _q3 }.OrderBy(q => q), questions.OrderBy(q => q));

            var sent = await HtmlOf(await client.GetAsync("/Partner/SentHomeworks"));
            Assert.Contains($"من نموذج: {ModelTitle}", sent);
        }

        [Fact]
        public async Task Send_ToForeignBatch_IsRejected_AndNothingIsSaved()
        {
            var html = await HtmlOf(await SendAsync(await OwnPartnerAsync(), Form(SendModelIntent.Send, _batchId, _foreignBatchId)));

            Assert.Contains("دفعات غير صالحة", html);
            Assert.Equal(0, await SetCountAsync());
        }

        [Fact]
        public async Task Send_AppliesExclusionsAndReplacementsFromForm()
        {
            var fields = Form(SendModelIntent.Send, _batchId);
            fields.Add(Field("ExcludedQuestionIds", _q2));
            fields.Add(Field($"Replacements[{_q1}]", _substitute));

            Assert.Equal(HttpStatusCode.Redirect, (await SendAsync(await OwnPartnerAsync(), fields)).StatusCode);

            var questions = await ReadAsync(db => db.Homeworks
                .Where(h => h.HomeworkSet.SourceProfessionalModelId == _modelId)
                .Select(h => h.QuestionId)
                .ToListAsync());
            Assert.Equal(new[] { _substitute, _q3 }.OrderBy(q => q), questions.OrderBy(q => q));
        }

        [Fact]
        public async Task Preview_KeepsChosenReplacementAndExclusionOnScreen()
        {
            var fields = Form(SendModelIntent.Preview, _batchId);
            fields.Add(Field("ExcludedQuestionIds", _q2));
            fields.Add(Field($"Replacements[{_q1}]", _substitute));

            var html = await HtmlOf(await SendAsync(await OwnPartnerAsync(), fields));

            Assert.Contains($"name=\"Replacements[{_q1}]\" value=\"{_substitute}\"", html);
            Assert.Contains($"{Marker} نص بديل", html);
            Assert.Matches($"value=\"{_q2}\"[^>]*checked", html);
        }

        [Fact]
        public async Task Send_WithReplacementFromAnotherLesson_IsRejected()
        {
            var fields = Form(SendModelIntent.Send, _batchId);
            fields.Add(Field($"Replacements[{_q1}]", _otherLessonQuestion));

            var html = await HtmlOf(await SendAsync(await OwnPartnerAsync(), fields));

            Assert.Contains("ليس من نفس الدرس", html);
            Assert.Equal(0, await SetCountAsync());
        }

        [Fact]
        public async Task ReplacementCandidates_ReturnSameLessonOnly_AndHideOtherPartnersModel()
        {
            var client = await OwnPartnerAsync();

            var html = await HtmlOf(await client.GetAsync($"{Base}/ReplacementCandidates/{_modelId}?questionId={_q1}"));
            Assert.Contains($"data-candidate=\"{_substitute}\"", html);
            Assert.DoesNotContain(_otherLessonQuestion.ToString(), html);
            Assert.DoesNotContain($"data-candidate=\"{_q2}\"", html);

            var foreign = await HtmlOf(await client.GetAsync($"{Base}/ReplacementCandidates/{_foreignModelId}?questionId={_q1}"));
            Assert.DoesNotContain("data-candidate", foreign);
        }

        [Fact]
        public async Task RepeatedSend_RequiresConfirmation_ThenSends()
        {
            var client = await OwnPartnerAsync();

            Assert.Equal(HttpStatusCode.Redirect, (await SendAsync(client, Form(SendModelIntent.Send, _batchId))).StatusCode);

            // خارج نافذة النقر المزدوج
            await ReadAsync(db => db.HomeworkSets
                .Where(h => h.SourceProfessionalModelId == _modelId)
                .ExecuteUpdateAsync(u => u.SetProperty(h => h.CreatedAt, h => h.CreatedAt.AddMinutes(-10))));

            var unconfirmed = await SendAsync(client, Form(SendModelIntent.Send, _batchId));
            var html = await HtmlOf(unconfirmed);

            Assert.Equal(HttpStatusCode.OK, unconfirmed.StatusCode);
            Assert.Contains("id=\"confirmWarnings\"", html);
            Assert.Contains("أُرسل هذا النموذج سابقًا", html);
            Assert.DoesNotContain("name=\"ConfirmWarnings\" value=\"true\" id=\"confirmWarnings\" checked", html);
            Assert.Equal(1, await SetCountAsync());

            var confirmed = Form(SendModelIntent.Send, _batchId);
            confirmed.Add(Field("ConfirmWarnings", "true"));
            Assert.Equal(HttpStatusCode.Redirect, (await SendAsync(client, confirmed)).StatusCode);
            Assert.Equal(2, await SetCountAsync());
        }

        [Fact]
        public async Task PostsWithoutAntiForgery_AreRejected()
        {
            var client = await OwnPartnerAsync();

            foreach (var path in new[] { "SendHomework", "Send", "SendCustomized" })
            {
                var response = await client.PostAsync($"{Base}/{path}", new FormUrlEncodedContent(Form(SendModelIntent.Send, _batchId)));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            Assert.Equal(0, await SetCountAsync());
        }

        // =====================================================
        // المسارات القديمة (PPM-S4.2)
        // =====================================================
        [Fact]
        public async Task LegacyRoutes_RedirectToSendHomework_AndExamModelStaysClosed()
        {
            var client = await OwnPartnerAsync();
            var target = $"{Base}/SendHomework/{_modelId}";

            foreach (var path in new[] { "Send", "SendCustomized" })
            {
                var response = await PostAsync(client, PartnerUser, $"{Base}/{path}", new[] { Field("modelId", _modelId) });
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.Equal(target, response.Headers.Location!.OriginalString);
            }

            var generation = await client.GetAsync($"/Partner/HomeworkGeneration/FromProfessionalModel?modelId={_modelId}");
            Assert.Equal(HttpStatusCode.Redirect, generation.StatusCode);
            Assert.Equal(target, generation.Headers.Location!.OriginalString);

            // كان Send يقبل نموذج الاختبار بطلب مصطنع
            var exam = await PostAsync(client, PartnerUser, $"{Base}/Send", new[] { Field("modelId", _examModelId) });
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(exam.Headers.Location!.OriginalString)).StatusCode);

            Assert.Equal(0, await SetCountAsync());
        }

        // =====================================================
        // ظهور الواجب عند الطالب (PPM-S4.3)
        // =====================================================
        private HttpClient StudentClient(string userId)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Student");
            return client;
        }

        [Fact]
        public async Task SentHomework_AppearsForRecipient_AndNonRecipientGetsNoQuestions()
        {
            Assert.Equal(HttpStatusCode.Redirect, (await SendAsync(await OwnPartnerAsync(), Form(SendModelIntent.Send, _batchId))).StatusCode);
            var setId = await ReadAsync(db => db.HomeworkSets.Where(h => h.SourceProfessionalModelId == _modelId).Select(h => h.Id).SingleAsync());

            var student = StudentClient(StudentUser);
            var list = await student.GetAsync("/Students/StudentHomeworkDashboard/AllHomeworks");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Contains(ModelTitle, await HtmlOf(list));

            Assert.Equal(HttpStatusCode.OK, (await student.GetAsync("/Students/StudentHomeworkDashboard/Dashboard")).StatusCode);

            var start = await student.GetAsync($"/Students/StudentHomeworkDashboard/Start?homeworkSetId={setId}");
            Assert.Equal(HttpStatusCode.OK, start.StatusCode);

            // المسجل في الدفعة غير المستلم: القائمة تعتمد التسجيل (موثق في known-risks)، والدخول يرفض بلا أسئلة
            var inactive = StudentClient(InactiveUser);
            var denied = await inactive.GetAsync($"/Students/StudentHomeworkDashboard/Start?homeworkSetId={setId}");
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("ErrorMessage", denied.Headers.Location!.OriginalString);

            Assert.Equal(0, await ReadAsync(db => db.Homeworks.CountAsync(h => h.StudentId == _inactiveId)));
        }

        private static class SendModelIntent
        {
            public const string Preview = QdratNew.ViewModels.Partner.SendModelHomeworkVm.IntentPreview;
            public const string Send = QdratNew.ViewModels.Partner.SendModelHomeworkVm.IntentSend;
        }
    }
}
