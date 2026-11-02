using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Placement;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S7.1 — تصليب مسار الطالب: لا تعديل بعد التسليم أو انتهاء الوقت، ولا كشف للإجابات أثناء الاختبار،
    // وAntiForgery على الحفظ والتسليم، وإجابة الطالب المزورة لا تُنفَّذ في صفحات الأدمن.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public class PartnerPlacementHardeningIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Marker = "PPLS7";
        private const string StudentUser = "ppl-s7-student";
        private const string StudentBase = "/Students/PlacementExams";

        private readonly CustomWebApplicationFactory _factory;
        private int _examId;
        private int _assignmentId;
        private int _studentId;
        private Guid _questionId;

        public PartnerPlacementHardeningIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var today = DateTime.Today;

            var partner = new Partner { Name = $"{Marker} شريك", Code = Marker, PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            var branch = new Branch { Name = $"{Marker} فرع", Partner = partner, Location = "-", City = "-", State = "-", Country = "-" };
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var curriculum = new Curriculum { Title = $"{Marker} منهج", Description = "-", CurriculumTypeName = "-" };
            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.AddRange(partner, branch, course, curriculum, unit);
            db.Users.Add(new ApplicationUser
            {
                Id = StudentUser,
                UserName = StudentUser,
                NormalizedUserName = StudentUser.ToUpperInvariant(),
                Email = $"{StudentUser}@test.local",
                NormalizedEmail = $"{StudentUser}@test.local".ToUpperInvariant(),
                FullName = StudentUser,
                NationalID = StudentUser,
                IsActive = true,
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            });
            await db.SaveChangesAsync();

            var section = new Section { Title = $"{Marker} محور", CurriculumId = curriculum.Id };
            db.Sections.Add(section);
            await db.SaveChangesAsync();

            var lesson = new Lesson { Title = $"{Marker} مؤشر", Content = "-", SectionId = section.Id, UnitId = unit.Id };
            db.Lessons.Add(lesson);
            await db.SaveChangesAsync();

            _questionId = Guid.NewGuid();
            db.Questions.Add(new Question
            {
                Id = _questionId,
                Title = $"{Marker} سؤال",
                ReferenceNumber = $"{Marker}{Guid.NewGuid():N}",
                CorrectAnswer = "أ",
                CurriculumId = curriculum.Id,
                SectionId = section.Id,
                LessonId = lesson.Id,
                Difficulty = DifficultyLevel.Medium,
                IsReviewed = true,
                IsComplete = true
            });

            var exam = new Exam { Title = $"{Marker} اختبار", Type = ExamType.LevelAssessment, DurationMinutes = 30, TotalQuestions = 1, IsActive = true, CourseId = course.Id, OwnerPartnerId = partner.Id };
            var student = new Student
            {
                NationalID = $"{Marker}1", FullName = $"طالب {Marker}", Gender = "ذكر", Age = 17,
                School = "-", Level = "-", UserId = StudentUser, Branch = branch, IsActiveForLearning = true
            };
            db.Exams.Add(exam);
            db.Students.Add(student);
            await db.SaveChangesAsync();

            db.ExamQuestions.Add(new ExamQuestion { ExamId = exam.Id, QuestionId = _questionId, Order = 1 });
            var assignment = new ExamAssignment { ExamId = exam.Id, StudentId = student.StudentID, AssignedAt = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddMinutes(30) };
            db.ExamAssignments.Add(assignment);
            db.ExamStudentStatuses.Add(new ExamStudentStatus { ExamId = exam.Id, StudentId = student.StudentID, AssignedAt = DateTime.UtcNow, Status = ExamStatus.Pending });
            await db.SaveChangesAsync();

            _examId = exam.Id;
            _assignmentId = assignment.Id;
            _studentId = student.StudentID;
        }

        public Task DisposeAsync() => CleanupAsync();

        private async Task CleanupAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            const string exams = "(SELECT Id FROM Exams WHERE Title LIKE 'PPLS7%')";

            foreach (var sql in new[]
            {
                "DELETE FROM SuspiciousActivities WHERE StudentId IN (SELECT StudentID FROM Students WHERE NationalID LIKE 'PPLS7%')",
                "DELETE FROM QuestionAttemptNew WHERE StudentId IN (SELECT StudentID FROM Students WHERE NationalID LIKE 'PPLS7%')",
                $"DELETE FROM ExamQuestions WHERE ExamId IN {exams}",
                $"DELETE FROM ExamStudentStatuses WHERE ExamId IN {exams}",
                $"DELETE FROM ExamAssignments WHERE ExamId IN {exams}",
                "DELETE FROM Exams WHERE Title LIKE 'PPLS7%'",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPLS7%'",
                "DELETE FROM Lessons WHERE Title LIKE 'PPLS7%'",
                "DELETE FROM Units WHERE Title LIKE 'PPLS7%'",
                "DELETE FROM Sections WHERE Title LIKE 'PPLS7%'",
                "DELETE FROM Curriculums WHERE Title LIKE 'PPLS7%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPLS7%'",
                "DELETE FROM Branches WHERE Name LIKE 'PPLS7%'",
                "DELETE FROM Partners WHERE Code = 'PPLS7'",
                "DELETE FROM Courses WHERE Name LIKE 'PPLS7%'",
                "DELETE FROM AspNetUsers WHERE Id LIKE 'ppl-s7-%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        private HttpClient StudentClient()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, StudentUser);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Student");
            return client;
        }

        // الحفظ والتسليم لا يحتاجان الجلسة: قائمة الأسئلة تُقرأ من القاعدة عند غيابها
        private async Task<HttpResponseMessage> PostWithTokenAsync(string url, HttpContent? content)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(StudentUser);
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, StudentUser);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Student");

            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Add("Cookie", cookie);
            request.Headers.Add("RequestVerificationToken", token);
            return await client.SendAsync(request);
        }

        private FormUrlEncodedContent AnswerForm(string answer = "أ") => new(new Dictionary<string, string>
        {
            ["id"] = _assignmentId.ToString(),
            ["q"] = _questionId.ToString(),
            ["nav"] = "next",
            ["SelectedOption"] = answer,
            ["timeTakenSeconds"] = "5"
        });

        private async Task UpdateStatusAsync(Action<ExamStudentStatus> change)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var status = await db.ExamStudentStatuses.SingleAsync(s => s.ExamId == _examId && s.StudentId == _studentId);
            change(status);
            await db.SaveChangesAsync();
        }

        private async Task<T> ReadAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private Task<int> AttemptsCountAsync()
            => ReadAsync(db => db.QuestionAttemptNew.CountAsync(a => a.ExamId == _examId && a.StudentId == _studentId));

        private static JsonElement JsonOf(string body) => JsonDocument.Parse(body).RootElement;

        // ---------- AntiForgery ----------
        [Fact]
        public async Task SubmitAnswer_And_SubmitFinal_WithoutAntiForgery_AreRejected()
        {
            var student = StudentClient();

            var answer = await student.PostAsync($"{StudentBase}/SubmitAnswerFetch", AnswerForm());
            Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);

            var final = await student.PostAsync($"{StudentBase}/SubmitFinal?id={_assignmentId}&forceSubmit=true", null);
            Assert.Equal(HttpStatusCode.BadRequest, final.StatusCode);

            Assert.Equal(0, await AttemptsCountAsync());
        }

        [Fact]
        public async Task SubmitAnswer_WithAntiForgery_DuringAttempt_IsSaved()
        {
            await UpdateStatusAsync(s => { s.StartedAt = DateTime.UtcNow; s.EndAt = DateTime.UtcNow.AddMinutes(30); s.Status = ExamStatus.InProgress; });

            var response = await PostWithTokenAsync($"{StudentBase}/SubmitAnswerFetch", AnswerForm());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, await AttemptsCountAsync());
        }

        // ---------- إغلاق المحاولة ----------
        [Fact]
        public async Task SubmitAnswer_AfterSubmission_IsRejected_AndAttemptUnchanged()
        {
            await UpdateStatusAsync(s => { s.StartedAt = DateTime.UtcNow; s.EndAt = DateTime.UtcNow.AddMinutes(30); s.Status = ExamStatus.InProgress; });
            await PostWithTokenAsync($"{StudentBase}/SubmitAnswerFetch", AnswerForm("ب"));
            await UpdateStatusAsync(s => { s.IsSubmitted = true; s.Status = ExamStatus.Completed; s.SubmittedAt = DateTime.UtcNow; });

            // بعد التسليم يرى الطالب الإجابة الصحيحة: محاولة تعديلها إلى «أ» لا تُقبل
            var response = await PostWithTokenAsync($"{StudentBase}/SubmitAnswerFetch", AnswerForm("أ"));
            var json = JsonOf(await response.Content.ReadAsStringAsync());

            Assert.False(json.GetProperty("success").GetBoolean());
            Assert.Contains("ResultPlacementExam", json.GetProperty("redirectUrl").GetString());

            var attempt = await ReadAsync(db => db.QuestionAttemptNew.AsNoTracking().SingleAsync(a => a.ExamId == _examId && a.StudentId == _studentId));
            Assert.Equal("ب", attempt.SelectedAnswer);
            Assert.False(attempt.IsCorrect);
        }

        [Fact]
        public async Task SubmitAnswer_AfterTimePlusGrace_IsRejected_ButWithinGrace_IsSaved()
        {
            await UpdateStatusAsync(s => { s.StartedAt = DateTime.UtcNow.AddMinutes(-30); s.EndAt = DateTime.UtcNow.AddSeconds(-5); s.Status = ExamStatus.InProgress; });
            Assert.Equal(HttpStatusCode.OK, (await PostWithTokenAsync($"{StudentBase}/SubmitAnswerFetch", AnswerForm())).StatusCode);
            Assert.Equal(1, await AttemptsCountAsync());

            await UpdateStatusAsync(s => s.EndAt = DateTime.UtcNow.AddMinutes(-5));
            var late = await PostWithTokenAsync($"{StudentBase}/SubmitAnswerFetch", AnswerForm("ب"));

            Assert.False(JsonOf(await late.Content.ReadAsStringAsync()).GetProperty("success").GetBoolean());
            var attempt = await ReadAsync(db => db.QuestionAttemptNew.AsNoTracking().SingleAsync(a => a.ExamId == _examId && a.StudentId == _studentId));
            Assert.Equal("أ", attempt.SelectedAnswer);
        }

        [Fact]
        public async Task SubmitAnswer_OnHiddenPartnerExam_IsRejected()
        {
            await UpdateStatusAsync(s => { s.StartedAt = DateTime.UtcNow; s.EndAt = DateTime.UtcNow.AddMinutes(30); s.Status = ExamStatus.InProgress; });

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Exams.Where(e => e.Id == _examId).ExecuteUpdateAsync(u => u.SetProperty(e => e.IsActive, false));
            }

            var response = await PostWithTokenAsync($"{StudentBase}/SubmitAnswerFetch", AnswerForm());

            Assert.False(JsonOf(await response.Content.ReadAsStringAsync()).GetProperty("success").GetBoolean());
            Assert.Equal(0, await AttemptsCountAsync());
        }

        [Fact]
        public async Task SubmitAnswer_OverlongAnswer_IsRejected_WithoutStorage()
        {
            await UpdateStatusAsync(s => { s.StartedAt = DateTime.UtcNow; s.EndAt = DateTime.UtcNow.AddMinutes(30); s.Status = ExamStatus.InProgress; });

            var response = await PostWithTokenAsync($"{StudentBase}/SubmitAnswerFetch", AnswerForm(new string('x', 5000)));

            Assert.False(JsonOf(await response.Content.ReadAsStringAsync()).GetProperty("success").GetBoolean());
            Assert.Equal(0, await AttemptsCountAsync());
        }

        [Fact]
        public async Task SubmitFinal_Twice_KeepsOriginalSubmissionTime()
        {
            await UpdateStatusAsync(s => { s.StartedAt = DateTime.UtcNow.AddMinutes(-10); s.EndAt = DateTime.UtcNow.AddMinutes(20); s.Status = ExamStatus.InProgress; });

            var first = await PostWithTokenAsync($"{StudentBase}/SubmitFinal?id={_assignmentId}&forceSubmit=true", null);
            Assert.True(JsonOf(await first.Content.ReadAsStringAsync()).GetProperty("success").GetBoolean());
            var submittedAt = await ReadAsync(db => db.ExamStudentStatuses.Where(s => s.ExamId == _examId && s.StudentId == _studentId).Select(s => s.SubmittedAt).SingleAsync());

            var second = await PostWithTokenAsync($"{StudentBase}/SubmitFinal?id={_assignmentId}&forceSubmit=true", null);
            Assert.True(JsonOf(await second.Content.ReadAsStringAsync()).GetProperty("success").GetBoolean());

            Assert.Equal(submittedAt, await ReadAsync(db => db.ExamStudentStatuses.Where(s => s.ExamId == _examId && s.StudentId == _studentId).Select(s => s.SubmittedAt).SingleAsync()));
        }

        // ---------- كشف الإجابات أثناء الاختبار ----------
        [Theory]
        [InlineData("ResultPlacementExam?assignmentId={0}")]
        [InlineData("PlacementReview?assignmentId={0}")]
        [InlineData("PlacementReport?assignmentId={0}")]
        [InlineData("ReviewLessonQuestions?lessonId=1&assignmentId={0}")]
        public async Task ResultPages_WhileAttemptRunning_RedirectToDashboard(string path)
        {
            await UpdateStatusAsync(s => { s.StartedAt = DateTime.UtcNow; s.EndAt = DateTime.UtcNow.AddMinutes(30); s.Status = ExamStatus.InProgress; });

            var response = await StudentClient().GetAsync($"{StudentBase}/{string.Format(path, _assignmentId)}");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("PlacementExamDashboard", response.Headers.Location!.ToString());
        }

        [Theory]
        [InlineData("ResultPlacementExam?assignmentId={0}")]
        [InlineData("PlacementReview?assignmentId={0}")]
        public async Task ResultPages_AfterSubmission_AreNotRedirected(string path)
        {
            await UpdateStatusAsync(s => { s.StartedAt = DateTime.UtcNow.AddMinutes(-10); s.IsSubmitted = true; s.Status = ExamStatus.Completed; s.SubmittedAt = DateTime.UtcNow; });

            var response = await StudentClient().GetAsync($"{StudentBase}/{string.Format(path, _assignmentId)}");

            Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        }

        [Fact]
        public async Task Dashboard_ShowsStart_NotResume_ForUnstartedExam()
        {
            // Razor يرمّز النص العربي، فالمقارنة بعد فك الترميز
            var html = WebUtility.HtmlDecode(await (await StudentClient().GetAsync($"{StudentBase}/PlacementExamDashboard")).Content.ReadAsStringAsync());

            Assert.Contains("بدء الاختبار", html);
            Assert.DoesNotContain("استكمال الاختبار", html);
        }

        // ---------- إجابة مزورة ----------
        [Theory]
        [InlineData("<img src=x onerror=alert(1)>", "")]
        [InlineData("<script>alert(1)</script>", "alert(1)")]
        [InlineData("x<sup>2</sup>", "x 2")]
        public void ToPlainText_NeverKeepsMarkup(string answer, string expectedText)
        {
            var text = PlacementReportText.ToPlainText(answer);

            Assert.DoesNotContain("<", text);
            if (expectedText.Length > 0)
                Assert.Equal(expectedText, text);
        }

        [Fact]
        public async Task SaveReviewBehavior_WritesOwnStatusOnly_AndRejectsOversizedLog()
        {
            var student = StudentClient();

            var ok = await student.PostAsync($"{StudentBase}/SaveReviewBehavior", JsonContent($"{{\"ExamAssignmentId\":{_assignmentId},\"ReviewNavLog\":\"[1]\"}}"));
            Assert.True(JsonOf(await ok.Content.ReadAsStringAsync()).GetProperty("success").GetBoolean());
            Assert.Equal("[1]", await ReadAsync(db => db.ExamStudentStatuses.Where(s => s.ExamId == _examId && s.StudentId == _studentId).Select(s => s.ReviewBehaviorJson).SingleAsync()));

            var big = await student.PostAsync($"{StudentBase}/SaveReviewBehavior", JsonContent($"{{\"ExamAssignmentId\":{_assignmentId},\"ReviewNavLog\":\"{new string('x', 20001)}\"}}"));
            Assert.False(JsonOf(await big.Content.ReadAsStringAsync()).GetProperty("success").GetBoolean());
        }

        private static StringContent JsonContent(string json) => new(json, System.Text.Encoding.UTF8, "application/json");
    }
}
