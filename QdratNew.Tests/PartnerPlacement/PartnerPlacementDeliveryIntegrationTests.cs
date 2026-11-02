using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S5 — الرحلة عبر HTTP حقيقي: الشريك يرسل، والطالب يحل في منطقته، والعزل بين الشركاء والطلاب.
    [Collection("SharedIntegrationDb")]
    public class PartnerPlacementDeliveryIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Marker = "PPLS5";
        private const string PartnerUser = "ppl-s5-partner";
        private const string OtherPartnerUser = "ppl-s5-partner-b";
        private const string StudentUser = "ppl-s5-student";
        private const string ArchivedUser = "ppl-s5-archived";
        private const string OtherStudentUser = "ppl-s5-student-b";
        private const string PartnerBase = "/Partner/PlacementExams";
        private const string StudentBase = "/Students/PlacementExams";
        private const string ExamTitle = "PPLS5 اختبار الشريك";

        private readonly CustomWebApplicationFactory _factory;
        private int _partnerId;
        private int _otherPartnerId;
        private int _batchId;
        private int _examId;
        private int _lessonId;
        private int _studentId;
        private int _archivedId;
        private int _otherStudentId;
        private Guid _questionId;

        public PartnerPlacementDeliveryIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var today = DateTime.Today;

            var partner = new Partner { Name = $"{Marker} شريك", Code = Marker, PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            var other = new Partner { Name = $"{Marker} شريك آخر", Code = $"{Marker}B", PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            var branch = new Branch { Name = $"{Marker} فرع", Partner = partner, Location = "-", City = "-", State = "-", Country = "-" };
            var otherBranch = new Branch { Name = $"{Marker} فرع آخر", Partner = other, Location = "-", City = "-", State = "-", Country = "-" };
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var curriculum = new Curriculum { Title = $"{Marker} منهج", Description = "-", CurriculumTypeName = "-" };
            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.AddRange(partner, other, branch, otherBranch, course, curriculum, unit);
            db.Users.AddRange(NewUser(PartnerUser), NewUser(OtherPartnerUser), NewUser(StudentUser), NewUser(ArchivedUser), NewUser(OtherStudentUser));
            await db.SaveChangesAsync();

            db.UserPartners.AddRange(
                new UserPartner { UserId = PartnerUser, PartnerId = partner.Id },
                new UserPartner { UserId = OtherPartnerUser, PartnerId = other.Id });

            var subscription = NewSubscription(partner.Id);
            var otherSubscription = NewSubscription(other.Id);
            db.PartnerSubscriptions.AddRange(subscription, otherSubscription);
            await db.SaveChangesAsync();
            db.PartnerSubscriptionPeriods.AddRange(NewPeriod(subscription.Id), NewPeriod(otherSubscription.Id));

            var section = new Section { Title = $"{Marker} محور", CurriculumId = curriculum.Id };
            db.Sections.Add(section);
            db.CourseCurriculums.Add(new CourseCurriculum { CourseId = course.Id, CurriculumId = curriculum.Id });
            await db.SaveChangesAsync();

            var lesson = new Lesson { Title = $"{Marker} مؤشر", Content = "-", SectionId = section.Id, UnitId = unit.Id };
            db.Lessons.Add(lesson);
            await db.SaveChangesAsync();

            _questionId = Guid.NewGuid();
            var secondQuestion = Guid.NewGuid();
            db.Questions.AddRange(
                NewQuestion(_questionId, "1", curriculum.Id, section.Id, lesson.Id),
                NewQuestion(secondQuestion, "2", curriculum.Id, section.Id, lesson.Id));

            var batch = new Batch { Name = $"{Marker} دفعة", BranchId = branch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            var otherBatch = new Batch { Name = $"{Marker} دفعة أخرى", BranchId = otherBranch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            var exam = new Exam { Title = ExamTitle, Type = ExamType.LevelAssessment, DurationMinutes = 30, TotalQuestions = 2, IsActive = true, CourseId = course.Id, OwnerPartnerId = partner.Id };
            db.Batches.AddRange(batch, otherBatch);
            db.Exams.Add(exam);

            var student = NewStudent("1", StudentUser, branch, active: true);
            var archived = NewStudent("2", ArchivedUser, branch, active: false);
            var otherStudent = NewStudent("3", OtherStudentUser, otherBranch, active: true);
            db.Students.AddRange(student, archived, otherStudent);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = student.StudentID, BatchId = batch.Id },
                new StudentBatchEnrollment { StudentID = archived.StudentID, BatchId = batch.Id },
                new StudentBatchEnrollment { StudentID = otherStudent.StudentID, BatchId = otherBatch.Id });
            db.ExamQuestions.AddRange(
                new ExamQuestion { ExamId = exam.Id, QuestionId = _questionId, Order = 1 },
                new ExamQuestion { ExamId = exam.Id, QuestionId = secondQuestion, Order = 2 });
            db.ExamAssignmentsToBatches.Add(new ExamAssignmentToBatch { ExamId = exam.Id, BatchId = batch.Id, Title = ExamTitle, CreatedAt = DateTime.UtcNow, AssignedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            _partnerId = partner.Id;
            _otherPartnerId = other.Id;
            _batchId = batch.Id;
            _examId = exam.Id;
            _lessonId = lesson.Id;
            _studentId = student.StudentID;
            _archivedId = archived.StudentID;
            _otherStudentId = otherStudent.StudentID;
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

        private static Student NewStudent(string suffix, string userId, Branch branch, bool active) => new()
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

        private static PartnerSubscription NewSubscription(int partnerId) => new()
        {
            PartnerId = partnerId,
            StartDate = DateTime.Today.AddDays(-1),
            EndDate = DateTime.Today.AddDays(30),
            CanUsePlacementExams = true,
            CanCreateExams = true
        };

        private static PartnerSubscriptionPeriod NewPeriod(int subscriptionId) => new()
        {
            PartnerSubscriptionId = subscriptionId,
            StartDate = DateTime.Today.AddDays(-1),
            EndDate = DateTime.Today.AddDays(30)
        };

        private static Question NewQuestion(Guid id, string suffix, int curriculumId, int sectionId, int lessonId) => new()
        {
            Id = id,
            Title = $"{Marker} نص السؤال {suffix}",
            ReferenceNumber = $"{Marker}{suffix}{id:N}",
            CorrectAnswer = "أ",
            CurriculumId = curriculumId,
            SectionId = sectionId,
            LessonId = lessonId,
            Difficulty = DifficultyLevel.Medium,
            IsReviewed = true,
            IsComplete = true,
            Options = new List<QuestionOption> { new() { Text = "أ" }, new() { Text = "ب" } }
        };

        private async Task CleanupAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPLS5%')";
            const string exams = "(SELECT Id FROM Exams WHERE Title LIKE 'PPLS5%')";
            const string partners = "(SELECT Id FROM Partners WHERE Code LIKE 'PPLS5%')";
            const string subscriptions = "(SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN " + partners + ")";

            foreach (var sql in new[]
            {
                $"DELETE FROM Notifications WHERE StudentID IN {students}",
                $"DELETE FROM SuspiciousActivities WHERE StudentId IN {students}",
                "DELETE FROM SystemLogs WHERE Description LIKE N'%PPLS5%'",
                $"DELETE FROM QuestionAttemptNew WHERE StudentId IN {students}",
                $"DELETE FROM ExamQuestions WHERE ExamId IN {exams}",
                $"DELETE FROM ExamStudentStatuses WHERE ExamId IN {exams}",
                $"DELETE FROM ExamAssignments WHERE ExamId IN {exams}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE ExamId IN {exams}",
                "DELETE FROM Exams WHERE Title LIKE 'PPLS5%'",
                "DELETE FROM QuestionOptions WHERE QuestionId IN (SELECT Id FROM Questions WHERE ReferenceNumber LIKE 'PPLS5%')",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPLS5%'",
                "DELETE FROM Lessons WHERE Title LIKE 'PPLS5%'",
                "DELETE FROM Units WHERE Title LIKE 'PPLS5%'",
                "DELETE FROM Sections WHERE Title LIKE 'PPLS5%'",
                "DELETE FROM CourseCurriculums WHERE CurriculumId IN (SELECT Id FROM Curriculums WHERE Title LIKE 'PPLS5%')",
                "DELETE FROM Curriculums WHERE Title LIKE 'PPLS5%'",
                "DELETE FROM StudentBatchEnrollments WHERE BatchId IN (SELECT Id FROM Batches WHERE Name LIKE 'PPLS5%')",
                "DELETE FROM Batches WHERE Name LIKE 'PPLS5%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPLS5%'",
                $"DELETE FROM PartnerSubscriptionPeriods WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptions WHERE Id IN {subscriptions}",
                $"DELETE FROM UserPartners WHERE PartnerId IN {partners}",
                "DELETE FROM Branches WHERE Name LIKE 'PPLS5%'",
                "DELETE FROM Partners WHERE Code LIKE 'PPLS5%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPLS5%'",
                "DELETE FROM AspNetUsers WHERE Id LIKE 'ppl-s5-%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        // اختيار الشريك عبر المسار الحقيقي، ثم إعادة كوكي الجلسة في كل طلب
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

        // كوكي الجلسة في Default، وكوكي الـ AntiForgery يجب أن يكون معها في نفس الهيدر
        private async Task<HttpResponseMessage> PartnerPostAsync(HttpClient client, string userId, string action, Dictionary<string, string> fields)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(userId);
            var session = client.DefaultRequestHeaders.GetValues("Cookie").Single();
            client.DefaultRequestHeaders.Remove("Cookie");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{PartnerBase}/{action}") { Content = new FormUrlEncodedContent(fields) };
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

        private Task<HttpResponseMessage> SendToStudentsAsync(HttpClient client, string userId = PartnerUser, int? examId = null)
            => PartnerPostAsync(client, userId, "SendExamToStudents", new Dictionary<string, string> { ["examId"] = (examId ?? _examId).ToString() });

        private HttpClient StudentClient(string userId)
        {
            // الجلسة تحفظ قائمة الأسئلة، فالكوكيز هنا مفعّلة
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Student");
            return client;
        }

        private static async Task<string> HtmlOf(HttpResponseMessage response)
            => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()); // Razor يرمّز العربية

        private async Task<T> ReadAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private Task<int> AssignmentIdOfAsync(int studentId)
            => ReadAsync(db => db.ExamAssignments.Where(a => a.ExamId == _examId && a.StudentId == studentId).Select(a => a.Id).SingleAsync());

        // PPL-S7.1: الحفظ والتسليم يتطلبان AntiForgery، ولا يحتاجان كوكي الجلسة
        private async Task<HttpResponseMessage> StudentPostAsync(string userId, string url, HttpContent? content)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(userId);
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Student");

            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Add("Cookie", cookie);
            request.Headers.Add("RequestVerificationToken", token);
            return await client.SendAsync(request);
        }

        private static FormUrlEncodedContent AnswerForm(int assignmentId, Guid questionId) => new(new Dictionary<string, string>
        {
            ["id"] = assignmentId.ToString(),
            ["q"] = questionId.ToString(),
            ["nav"] = "next",
            ["SelectedOption"] = "أ",
            ["timeTakenSeconds"] = "5"
        });

        [Fact]
        public async Task ConfirmSend_ShowsActiveStudentCount_AndDoesNotDeliver()
        {
            var client = await OwnPartnerAsync();

            var response = await client.GetAsync($"{PartnerBase}/ConfirmSend?examId={_examId}");
            var html = await HtmlOf(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("إرسال الاختبار لـ 1 طالب", html); // المؤرشف لا يُحسب
            Assert.False(await ReadAsync(db => db.ExamAssignments.AnyAsync(a => a.ExamId == _examId)));
        }

        [Fact]
        public async Task PartnerSends_StudentSolves_EndToEnd()
        {
            var partner = await OwnPartnerAsync();

            var send = await SendToStudentsAsync(partner);
            Assert.Equal(HttpStatusCode.Redirect, send.StatusCode);
            Assert.Contains("PlacementDetails", send.Headers.Location!.ToString());

            // يصل لطالب الشريك النشط فقط
            var assigned = await ReadAsync(db => db.ExamAssignments.Where(a => a.ExamId == _examId).Select(a => a.StudentId).ToListAsync());
            Assert.Equal(new[] { _studentId }, assigned);

            var notified = await ReadAsync(db => db.Notifications
                .Where(n => n.StudentID == _studentId || n.StudentID == _archivedId || n.StudentID == _otherStudentId)
                .Select(n => new { n.StudentID, n.TargetUrl, n.Category })
                .ToListAsync());
            var notification = Assert.Single(notified);
            Assert.Equal(_studentId, notification.StudentID);
            Assert.Equal($"{StudentBase}/PlacementExamDashboard", notification.TargetUrl);
            Assert.Equal(NotificationCategory.Exam, notification.Category);

            // تفاصيل الشريك: إعادة الإرسال للطالب، وإجراءات الدفعة
            var details = await HtmlOf(await partner.GetAsync($"{PartnerBase}/PlacementDetails?id={_examId}"));
            Assert.Contains("ResendExamToStudent", details);
            Assert.Contains("SendExamToNewStudents", details);
            Assert.Contains($"{Marker} دفعة", details);

            // ---------- الطالب ----------
            var student = StudentClient(StudentUser);
            var assignmentId = await AssignmentIdOfAsync(_studentId);

            var dashboard = await student.GetAsync($"{StudentBase}/PlacementExamDashboard");
            Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
            Assert.Contains($"assignmentId={assignmentId}", await HtmlOf(dashboard));

            var start = await student.GetAsync($"{StudentBase}/StartPlacementExam?assignmentId={assignmentId}");
            Assert.Equal(HttpStatusCode.OK, start.StatusCode);

            var answer = await StudentPostAsync(StudentUser, $"{StudentBase}/SubmitAnswerFetch", AnswerForm(assignmentId, _questionId));
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            Assert.True(await ReadAsync(db => db.QuestionAttemptNew.AnyAsync(a =>
                a.StudentId == _studentId && a.ExamId == _examId && a.ExamAssignmentId == assignmentId && a.IsCorrect)));

            var final = await StudentPostAsync(StudentUser, $"{StudentBase}/SubmitFinal?id={assignmentId}&forceSubmit=true", null);
            Assert.True(JsonDocument.Parse(await final.Content.ReadAsStringAsync()).RootElement.GetProperty("success").GetBoolean());

            var status = await ReadAsync(db => db.ExamStudentStatuses.AsNoTracking().SingleAsync(s => s.ExamId == _examId && s.StudentId == _studentId));
            Assert.True(status.IsSubmitted);
            Assert.Equal(ExamStatus.Completed, status.Status);

            Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"{StudentBase}/ResultPlacementExam?assignmentId={assignmentId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"{StudentBase}/PlacementReport?assignmentId={assignmentId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"{StudentBase}/PlacementReview?assignmentId={assignmentId}")).StatusCode);

            // ---------- إعادة الإرسال: تعيين واحد جديد، وتُمسح المحاولات ----------
            var resend = await PartnerPostAsync(partner, PartnerUser, "ResendExamToStudent", new Dictionary<string, string>
            {
                ["examId"] = _examId.ToString(),
                ["studentId"] = _studentId.ToString()
            });
            Assert.Equal(HttpStatusCode.Redirect, resend.StatusCode);

            var newAssignmentId = await AssignmentIdOfAsync(_studentId); // Single: لا تكرار
            Assert.NotEqual(assignmentId, newAssignmentId);
            Assert.False(await ReadAsync(db => db.QuestionAttemptNew.AnyAsync(a => a.StudentId == _studentId && a.ExamId == _examId)));
        }

        [Fact]
        public async Task SendNewStudents_ReachesOnlyTheNewOne()
        {
            var partner = await OwnPartnerAsync();
            await SendToStudentsAsync(partner);

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Students.Where(s => s.StudentID == _archivedId).ExecuteUpdateAsync(u => u.SetProperty(s => s.IsActiveForLearning, true));
            }

            var response = await PartnerPostAsync(partner, PartnerUser, "SendExamToNewStudents", new Dictionary<string, string>
            {
                ["examId"] = _examId.ToString(),
                ["batchId"] = _batchId.ToString()
            });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var counts = await ReadAsync(db => db.ExamAssignments.Where(a => a.ExamId == _examId).GroupBy(a => a.StudentId).Select(g => g.Count()).ToListAsync());
            Assert.Equal(new[] { 1, 1 }, counts);
        }

        [Fact]
        public async Task OtherPartner_CannotSendOrResendOrViewConfirm()
        {
            var other = await PartnerClientAsync(OtherPartnerUser, _otherPartnerId);

            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{PartnerBase}/ConfirmSend?examId={_examId}")).StatusCode);

            var send = await SendToStudentsAsync(other, OtherPartnerUser);
            Assert.Contains("Review", send.Headers.Location!.ToString()); // ثم 404 لأنها خارج نطاقه
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{PartnerBase}/Review?examId={_examId}")).StatusCode);

            var resend = await PartnerPostAsync(other, OtherPartnerUser, "ResendExamToStudent", new Dictionary<string, string>
            {
                ["examId"] = _examId.ToString(),
                ["studentId"] = _studentId.ToString()
            });
            Assert.Equal(HttpStatusCode.NotFound, resend.StatusCode);

            Assert.False(await ReadAsync(db => db.ExamAssignments.AnyAsync(a => a.ExamId == _examId)));
            Assert.False(await ReadAsync(db => db.ExamAssignmentsToBatches.AnyAsync(x => x.ExamId == _examId && x.IsSentToStudents)));
        }

        [Fact]
        public async Task SendActions_WithoutAntiForgery_AreRejected()
        {
            var partner = await OwnPartnerAsync();

            var response = await partner.PostAsync($"{PartnerBase}/SendExamToStudents",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["examId"] = _examId.ToString() }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await ReadAsync(db => db.ExamAssignments.AnyAsync(a => a.ExamId == _examId)));
        }

        [Fact]
        public async Task OtherStudent_CannotReadOrAnswerAssignmentOfAnotherStudent()
        {
            await SendToStudentsAsync(await OwnPartnerAsync());
            var assignmentId = await AssignmentIdOfAsync(_studentId);
            var intruder = StudentClient(OtherStudentUser);

            Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"{StudentBase}/StartPlacementExam?assignmentId={assignmentId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"{StudentBase}/VerifyPlacementExamCode?assignmentId={assignmentId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"{StudentBase}/PlacementReport?assignmentId={assignmentId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"{StudentBase}/ReviewLessonQuestions?lessonId={_lessonId}&assignmentId={assignmentId}")).StatusCode);
            Assert.DoesNotContain(ExamTitle, await HtmlOf(await intruder.GetAsync($"{StudentBase}/ResultPlacementExam?assignmentId={assignmentId}")));
            Assert.DoesNotContain(ExamTitle, await HtmlOf(await intruder.GetAsync($"{StudentBase}/PlacementReview?assignmentId={assignmentId}")));
            Assert.DoesNotContain(ExamTitle, await HtmlOf(await intruder.GetAsync($"{StudentBase}/PlacementExamDashboard")));

            // قبل PPL-S5.3 كان هذا الطلب يُرجع نص السؤال ويسجّل محاولة على اختبار لا يخصه
            var answer = await StudentPostAsync(OtherStudentUser, $"{StudentBase}/SubmitAnswerFetch", AnswerForm(assignmentId, _questionId));
            var body = await HtmlOf(answer);
            Assert.False(JsonDocument.Parse(body).RootElement.GetProperty("success").GetBoolean());
            Assert.DoesNotContain("نص السؤال", body);

            var final = await StudentPostAsync(OtherStudentUser, $"{StudentBase}/SubmitFinal?id={assignmentId}&forceSubmit=true", null);
            Assert.False(JsonDocument.Parse(await final.Content.ReadAsStringAsync()).RootElement.GetProperty("success").GetBoolean());

            Assert.False(await ReadAsync(db => db.QuestionAttemptNew.AnyAsync(a => a.StudentId == _otherStudentId)));
            Assert.False(await ReadAsync(db => db.ExamStudentStatuses.AnyAsync(s => s.ExamId == _examId && s.IsSubmitted)));
        }

        [Fact]
        public async Task HiddenPartnerExam_DisappearsFromStudent_AndCannotBeResent()
        {
            var partner = await OwnPartnerAsync();
            await SendToStudentsAsync(partner);
            var assignmentId = await AssignmentIdOfAsync(_studentId);

            var hide = await PartnerPostAsync(partner, PartnerUser, "HideExam", new Dictionary<string, string> { ["id"] = _examId.ToString() });
            Assert.Equal(HttpStatusCode.Redirect, hide.StatusCode);

            var student = StudentClient(StudentUser);
            // عنوان الاختبار قد يبقى في جرس الإشعارات؛ المهم أن كارته ورابط بدئه اختفيا
            Assert.DoesNotContain($"assignmentId={assignmentId}", await HtmlOf(await student.GetAsync($"{StudentBase}/PlacementExamDashboard")));
            Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"{StudentBase}/StartPlacementExam?assignmentId={assignmentId}")).StatusCode);

            var resend = await PartnerPostAsync(partner, PartnerUser, "ResendExamToStudent", new Dictionary<string, string>
            {
                ["examId"] = _examId.ToString(),
                ["studentId"] = _studentId.ToString()
            });
            Assert.Contains("Review", resend.Headers.Location!.ToString());
            Assert.Equal(assignmentId, await AssignmentIdOfAsync(_studentId));
        }

        [Fact]
        public async Task SentPlacementExam_DoesNotLeakIntoRegularExamPages()
        {
            var partner = await OwnPartnerAsync();
            await SendToStudentsAsync(partner);

            var list = await partner.GetAsync("/Partner/PartnerExam/SentExams");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.DoesNotContain(ExamTitle, await HtmlOf(list));

            var reports = await partner.GetAsync($"/Partner/Reports/BatchExams?batchId={_batchId}");
            Assert.Equal(HttpStatusCode.OK, reports.StatusCode);
            Assert.DoesNotContain(ExamTitle, await HtmlOf(reports));

            // جرس الإشعارات في الـ layout يذكر العنوان، والمطلوب هنا كروت الصفحة نفسها
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Notifications.Where(n => n.StudentID == _studentId).ExecuteDeleteAsync();
            }

            var student = await StudentClient(StudentUser).GetAsync("/Students/Exams/Index");
            Assert.DoesNotContain(ExamTitle, await HtmlOf(student));
        }
    }
}
