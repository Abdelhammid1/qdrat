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
    // PPM-S6 — CreateModelHomework/CreateModelExam عبر HTTP حقيقي: الصلاحية والتوكن والرسائل والتحويلات،
    // وعرض التحذيرات مع بقاء الاختيارات، وعرض موعد البطاقة عند الطالب بتوقيت السعودية.
    [Collection("SharedIntegrationDb")]
    public class AdminModelSendIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Marker = "PPMS6H";
        private const string OwnerUser = "ppm-s6-owner";
        private const string StudentUser = "ppm-s6-student";
        private const string Base = "/Admin/AdminLessonCompletions";

        private readonly CustomWebApplicationFactory _factory;
        private int _batchId;
        private int _homeworkModelId;
        private int _examModelId;
        private int _studentId;

        public AdminModelSendIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var branch = new Branch { Name = $"{Marker} فرع", Location = "-", City = "-", State = "-", Country = "-" };
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var curriculum = new Curriculum { Title = $"{Marker} منهج", Description = "-", CurriculumTypeName = "-" };
            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.AddRange(branch, course, curriculum, unit);
            db.Users.AddRange(NewUser(OwnerUser), NewUser(StudentUser));
            await db.SaveChangesAsync();

            var section = new Section { Title = $"{Marker} محور", CurriculumId = curriculum.Id };
            db.Sections.Add(section);
            await db.SaveChangesAsync();

            var lesson = new Lesson { Title = $"{Marker} درس", Content = "-", SectionId = section.Id, UnitId = unit.Id };
            db.Lessons.Add(lesson);
            await db.SaveChangesAsync();

            var question = new Question
            {
                Id = Guid.NewGuid(),
                Title = $"{Marker} نص",
                ReferenceNumber = $"{Marker}Q1",
                CorrectAnswer = "أ",
                CurriculumId = curriculum.Id,
                SectionId = section.Id,
                LessonId = lesson.Id,
                IsReviewed = true,
                IsComplete = true
            };
            db.Questions.Add(question);

            ProfessionalModel NewModel(string title, ProfessionalModelType type) => new()
            {
                Title = $"{Marker} {title}",
                Description = "-",
                CreatedBy = "-",
                CreatedAt = DateTime.UtcNow,
                ModelType = type,
                CurriculumId = curriculum.Id,
                Questions = new List<ProfessionalModelQuestion> { new() { QuestionId = question.Id, OrderNumber = 1 } }
            };

            var homeworkModel = NewModel("واجب", ProfessionalModelType.Homework);
            var examModel = NewModel("اختبار", ProfessionalModelType.Exam);
            db.ProfessionalModels.AddRange(homeworkModel, examModel);

            var batch = new Batch { Name = $"{Marker} دفعة", BranchId = branch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            db.Batches.Add(batch);

            var student = new Student
            {
                NationalID = $"{Marker}1",
                FullName = $"{Marker} طالب",
                Gender = "ذكر",
                Age = 17,
                School = "-",
                Level = "-",
                UserId = StudentUser,
                Branch = branch,
                IsActiveForLearning = true
            };
            db.Students.Add(student);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = student.StudentID, BatchId = batch.Id });
            await db.SaveChangesAsync();

            _batchId = batch.Id;
            _homeworkModelId = homeworkModel.Id;
            _examModelId = examModel.Id;
            _studentId = student.StudentID;
        }

        public Task DisposeAsync() => CleanupAsync();

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

        private async Task CleanupAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPMS6H%')";
            const string batches = "(SELECT Id FROM Batches WHERE Name LIKE 'PPMS6H%')";
            const string sets = "(SELECT Id FROM HomeworkSets WHERE BatchId IN " + batches + ")";
            const string assignments = "(SELECT Id FROM ExamAssignmentsToBatches WHERE BatchId IN " + batches + ")";
            const string models = "(SELECT Id FROM ProfessionalModels WHERE Title LIKE 'PPMS6H%')";

            foreach (var sql in new[]
            {
                $"DELETE FROM Notifications WHERE StudentID IN {students}",
                "DELETE FROM SystemLogs WHERE Description LIKE N'%PPMS6H%'",
                $"DELETE FROM ExamStudentStatuses WHERE StudentId IN {students}",
                $"DELETE FROM ExamQuestions WHERE ExamAssignmentId IN {assignments}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE BatchId IN {batches}",
                $"DELETE FROM Exams WHERE SourceProfessionalModelId IN {models}",
                $"DELETE FROM Homeworks WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSetStudents WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSets WHERE BatchId IN {batches}",
                $"DELETE FROM ProfessionalModelQuestions WHERE ModelId IN {models}",
                $"DELETE FROM ProfessionalModels WHERE Id IN {models}",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPMS6H%'",
                "DELETE FROM Lessons WHERE Title LIKE 'PPMS6H%'",
                "DELETE FROM Units WHERE Title LIKE 'PPMS6H%'",
                "DELETE FROM Sections WHERE Title LIKE 'PPMS6H%'",
                "DELETE FROM Curriculums WHERE Title LIKE 'PPMS6H%'",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {batches}",
                "DELETE FROM Batches WHERE Name LIKE 'PPMS6H%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPMS6H%'",
                "DELETE FROM Branches WHERE Name LIKE 'PPMS6H%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPMS6H%'",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'ppm-s6-%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        private HttpClient Client(string userId, string role)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
            return client;
        }

        private HttpClient Owner() => Client(OwnerUser, "Owner");

        private HttpRequestMessage Post(string url, IEnumerable<KeyValuePair<string, string>> fields)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(OwnerUser);
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("Cookie", cookie);
            request.Headers.Add("RequestVerificationToken", token);
            return request;
        }

        private static KeyValuePair<string, string> Field(string name, object value) => new(name, value.ToString()!);

        private static string SaudiLocal(int days) => DateTime.UtcNow.AddHours(3).AddDays(days).ToString("yyyy-MM-ddTHH:mm");

        private List<KeyValuePair<string, string>> HomeworkForm(params KeyValuePair<string, string>[] extra)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                Field("ModelId", _homeworkModelId),
                Field("SelectedBatchIds", _batchId),
                Field("Title", $"{Marker} عنوان"),
                Field("StartAt", SaudiLocal(0)),
                Field("EndAt", SaudiLocal(2))
            };
            fields.AddRange(extra);
            return fields;
        }

        private List<KeyValuePair<string, string>> ExamForm(params KeyValuePair<string, string>[] extra)
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                Field("ModelId", _examModelId),
                Field("SelectedBatchIds", _batchId),
                Field("SendToBatch", "true"),
                Field("DurationMinutes", 40),
                Field("StartAt", SaudiLocal(0)),
                Field("EndAt", SaudiLocal(2))
            };
            fields.AddRange(extra);
            return fields;
        }

        private static async Task<string> HtmlOf(HttpResponseMessage response)
            => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        private async Task<T> ReadAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        [Theory]
        [InlineData("CreateModelHomework")]
        [InlineData("CreateModelExam")]
        public async Task Post_WithoutToken_IsRejected(string action)
        {
            var response = await Owner().PostAsync($"{Base}/{action}", new FormUrlEncodedContent(ExamForm()));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("CreateModelHomework", "Student")]
        [InlineData("CreateModelExam", "Student")]
        [InlineData("CreateModelHomework", "Partner")]
        [InlineData("CreateModelExam", "Partner")]
        public async Task Post_FromNonAdminRole_IsDenied_NoWrites(string action, string role)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(StudentUser);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/{action}")
            {
                Content = new FormUrlEncodedContent(action == "CreateModelExam" ? ExamForm() : HomeworkForm())
            };
            request.Headers.Add("Cookie", cookie);
            request.Headers.Add("RequestVerificationToken", token);

            var response = await Client(StudentUser, role).SendAsync(request);

            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized });
            Assert.DoesNotContain("ModelAssignments", response.Headers.Location?.ToString() ?? string.Empty);
            Assert.Equal(0, await ReadAsync(db => db.HomeworkSets.CountAsync(h => h.BatchId == _batchId)));
            Assert.Equal(0, await ReadAsync(db => db.ExamAssignmentsToBatches.CountAsync(a => a.BatchId == _batchId)));
        }

        [Theory]
        [InlineData("CreateModelHomework")]
        [InlineData("CreateModelExam")]
        public async Task Get_RendersScreen(string action)
        {
            var response = await Owner().GetAsync($"{Base}/{action}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Homework_Success_RedirectsAsBefore_AndTracksSource()
        {
            var response = await Owner().SendAsync(Post($"{Base}/CreateModelHomework", HomeworkForm()));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("ModelAssignments", response.Headers.Location!.ToString());

            Assert.Equal(1, await ReadAsync(db => db.HomeworkSets.CountAsync(h => h.SourceProfessionalModelId == _homeworkModelId && h.BatchId == _batchId && h.AssignedByUserId == OwnerUser)));
        }

        [Fact]
        public async Task Exam_Success_RedirectsAsBefore_AndIsAdminOwned()
        {
            var response = await Owner().SendAsync(Post($"{Base}/CreateModelExam", ExamForm()));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("ModelAssignments", response.Headers.Location!.ToString());

            var exam = await ReadAsync(db => db.Exams.AsNoTracking().SingleAsync(e => e.SourceProfessionalModelId == _examModelId));
            Assert.Null(exam.OwnerPartnerId);

            var list = await HtmlOf(await Owner().GetAsync($"{Base}/ModelAssignments"));
            Assert.DoesNotContain("حدث خطأ", list);
        }

        [Fact]
        public async Task Homework_WrongModelType_RedirectsWithOldMessage_NoWrites()
        {
            var fields = HomeworkForm();
            fields[0] = Field("ModelId", _examModelId);

            var response = await Owner().SendAsync(Post($"{Base}/CreateModelHomework", fields));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(0, await ReadAsync(db => db.HomeworkSets.CountAsync(h => h.BatchId == _batchId)));
        }

        [Fact]
        public async Task Exam_InvalidDates_ShowsMessageOnSameScreen_AndKeepsInput()
        {
            var fields = ExamForm();
            fields.RemoveAll(f => f.Key == "EndAt");
            fields.Add(Field("EndAt", SaudiLocal(-2)));
            fields.RemoveAll(f => f.Key == "DurationMinutes");
            fields.Add(Field("DurationMinutes", 47));

            var response = await Owner().SendAsync(Post($"{Base}/CreateModelExam", fields));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await HtmlOf(response);
            Assert.Contains("موعد النهاية يجب أن يكون بعد موعد البداية", html);
            Assert.Contains("value=\"47\"", html);
            Assert.DoesNotContain("field-validation-error", html);
            Assert.Equal(0, await ReadAsync(db => db.Exams.CountAsync(e => e.SourceProfessionalModelId == _examModelId)));
        }

        [Fact]
        public async Task Exam_DurationOutOfRange_IsRejected_NoWrites()
        {
            var fields = ExamForm();
            fields.RemoveAll(f => f.Key == "DurationMinutes");
            fields.Add(Field("DurationMinutes", 1));

            var response = await Owner().SendAsync(Post($"{Base}/CreateModelExam", fields));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("مدة الاختبار يجب أن تكون بين", await HtmlOf(response));
            Assert.Equal(0, await ReadAsync(db => db.Exams.CountAsync(e => e.SourceProfessionalModelId == _examModelId)));
        }

        [Fact]
        public async Task Repeat_ShowsWarningsWithoutWriting_ThenConfirmSends()
        {
            Assert.Equal(HttpStatusCode.Redirect, (await Owner().SendAsync(Post($"{Base}/CreateModelExam", ExamForm()))).StatusCode);

            await ReadAsync(db => db.ExamAssignmentsToBatches
                .Where(a => a.BatchId == _batchId)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.CreatedAt, DateTime.UtcNow.AddDays(-1))));

            var warned = await Owner().SendAsync(Post($"{Base}/CreateModelExam", ExamForm()));
            Assert.Equal(HttpStatusCode.OK, warned.StatusCode);
            var html = await HtmlOf(warned);
            Assert.Contains("modelSendWarnings", html);
            Assert.Contains($"{Marker} دفعة", html);
            Assert.Equal(1, await ReadAsync(db => db.Exams.CountAsync(e => e.SourceProfessionalModelId == _examModelId)));

            var confirmed = await Owner().SendAsync(Post($"{Base}/CreateModelExam", ExamForm(Field("ConfirmWarnings", "true"))));
            Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
            Assert.Equal(2, await ReadAsync(db => db.Exams.CountAsync(e => e.SourceProfessionalModelId == _examModelId)));
        }

        [Fact]
        public async Task Repeat_Homework_WithinSixtySeconds_IsBlockedEvenIfConfirmed()
        {
            Assert.Equal(HttpStatusCode.Redirect, (await Owner().SendAsync(Post($"{Base}/CreateModelHomework", HomeworkForm()))).StatusCode);

            var again = await Owner().SendAsync(Post($"{Base}/CreateModelHomework", HomeworkForm(Field("ConfirmWarnings", "true"))));

            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
            Assert.Contains("قبل لحظات", await HtmlOf(again));
            Assert.Equal(1, await ReadAsync(db => db.HomeworkSets.CountAsync(h => h.SourceProfessionalModelId == _homeworkModelId)));
        }

        // الخدمة تخزن UTC، وبطاقة الطالب تعرض السعودية (known-risks: كانت قبل الموعد بثلاث ساعات)
        [Fact]
        public async Task StudentCard_ShowsEndDateInSaudiTime()
        {
            var endLocal = DateTime.UtcNow.AddHours(3).AddDays(2);
            var fields = ExamForm(Field("Title", $"{Marker} بطاقة"));
            fields.RemoveAll(f => f.Key == "EndAt");
            fields.Add(Field("EndAt", endLocal.ToString("yyyy-MM-ddTHH:mm")));

            Assert.Equal(HttpStatusCode.Redirect, (await Owner().SendAsync(Post($"{Base}/CreateModelExam", fields))).StatusCode);

            var stored = await ReadAsync(db => db.ExamAssignmentsToBatches.AsNoTracking()
                .Where(a => a.BatchId == _batchId)
                .Select(a => a.EndAt)
                .SingleAsync());
            Assert.Equal(endLocal.AddHours(-3).ToString("yyyy-MM-ddTHH:mm"), stored!.Value.ToString("yyyy-MM-ddTHH:mm"));

            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<QdratNew.Services.Interfaces.IStudentExamStatusService>();
            var courseId = await ReadAsync(db => db.Batches.Where(b => b.Id == _batchId).Select(b => b.CourseId).SingleAsync());

            var cards = await service.GetRequiredExamsAsync(_studentId, courseId, _batchId);
            var card = Assert.Single(cards);
            Assert.Equal(endLocal.ToString("yyyy-MM-ddTHH:mm"), card.EndAt!.Value.ToString("yyyy-MM-ddTHH:mm"));

            // شاشة التعديل تعرض وتحفظ بتوقيت السعودية، فتبقى البطاقة صحيحة بعد التعديل
            var assignmentId = await ReadAsync(db => db.ExamAssignmentsToBatches.Where(a => a.BatchId == _batchId).Select(a => a.Id).SingleAsync());
            var editPage = await HtmlOf(await Owner().GetAsync($"{Base}/EditProfessionalExam/{assignmentId}"));
            Assert.Contains(endLocal.ToString("yyyy-MM-ddTHH:mm"), editPage);

            var editedLocal = endLocal.AddDays(1);
            var saved = await Owner().SendAsync(Post($"{Base}/EditProfessionalExam", new[]
            {
                Field("Id", assignmentId),
                Field("Title", $"{Marker} بطاقة"),
                Field("BatchId", _batchId),
                Field("BatchName", "-"),
                Field("TotalQuestions", 1),
                Field("DurationMinutes", 40),
                Field("IsOnline", "true"),
                Field("EndAt", editedLocal.ToString("yyyy-MM-ddTHH:mm"))
            }));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

            var edited = Assert.Single(await service.GetRequiredExamsAsync(_studentId, courseId, _batchId));
            Assert.Equal(editedLocal.ToString("yyyy-MM-ddTHH:mm"), edited.EndAt!.Value.ToString("yyyy-MM-ddTHH:mm"));
        }
    }
}
