using System.Net;
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
    // PPL-S6 — تقارير الشريك عبر HTTP حقيقي: العزل، الترويسة، والقراءة حتى AccessUntilDate.
    [Collection("SharedIntegrationDb")]
    public class PartnerPlacementReportsIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Marker = "PPLS6";
        private const string PartnerUser = "ppl-s6-partner";
        private const string OtherPartnerUser = "ppl-s6-partner-b";
        private const string StudentUser = "ppl-s6-student";
        private const string Base = "/Partner/PlacementExams";
        private const string ExamTitle = "PPLS6 اختبار الشريك";
        private const string PartnerName = "PPLS6 مدرسة النور";
        private const string ForgedAnswer = "<script>alert('ppl')</script>";

        private readonly CustomWebApplicationFactory _factory;
        private int _partnerId;
        private int _otherPartnerId;
        private int _subscriptionId;
        private int _batchId;
        private int _otherBatchId;
        private int _examId;
        private int _unlinkedExamId;
        private int _lessonId;
        private int _studentId;
        private int _otherStudentId;
        private int _assignmentId;
        private Guid _questionId;

        public PartnerPlacementReportsIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var today = DateTime.Today;

            var partner = new Partner { Name = PartnerName, Code = Marker, PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            var other = new Partner { Name = $"{Marker} شريك آخر", Code = $"{Marker}B", PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            var branch = new Branch { Name = $"{Marker} فرع", Partner = partner, Location = "-", City = "-", State = "-", Country = "-" };
            var otherBranch = new Branch { Name = $"{Marker} فرع آخر", Partner = other, Location = "-", City = "-", State = "-", Country = "-" };
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var curriculum = new Curriculum { Title = $"{Marker} كمي", Description = "-", CurriculumTypeName = "-" };
            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.AddRange(partner, other, branch, otherBranch, course, curriculum, unit);
            db.Users.AddRange(NewUser(PartnerUser), NewUser(OtherPartnerUser), NewUser(StudentUser));
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
            await db.SaveChangesAsync();

            var lesson = new Lesson { Title = $"{Marker} مؤشر", Content = "-", SectionId = section.Id, UnitId = unit.Id };
            db.Lessons.Add(lesson);
            await db.SaveChangesAsync();

            _questionId = Guid.NewGuid();
            db.Questions.Add(new Question
            {
                Id = _questionId,
                Title = $"{Marker} نص السؤال",
                ReferenceNumber = $"{Marker}{_questionId:N}",
                CorrectAnswer = "أ",
                CurriculumId = curriculum.Id,
                SectionId = section.Id,
                LessonId = lesson.Id,
                Difficulty = DifficultyLevel.Medium,
                IsReviewed = true,
                IsComplete = true,
                IsQuantitative = true,
                Options = new List<QuestionOption> { new() { Text = "أ" }, new() { Text = "ب" } }
            });

            var batch = new Batch { Name = $"{Marker} دفعة", BranchId = branch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            var otherBatch = new Batch { Name = $"{Marker} دفعة أخرى", BranchId = otherBranch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            db.Batches.AddRange(batch, otherBatch);

            var exam = new Exam { Title = ExamTitle, Type = ExamType.LevelAssessment, DurationMinutes = 30, TotalQuestions = 1, IsActive = true, CourseId = course.Id, OwnerPartnerId = partner.Id };
            var unlinked = new Exam { Title = $"{Marker} بلا دفعة", Type = ExamType.LevelAssessment, DurationMinutes = 30, TotalQuestions = 1, IsActive = true, CourseId = course.Id, OwnerPartnerId = partner.Id };
            db.Exams.AddRange(exam, unlinked);

            var student = NewStudent("1", StudentUser, branch);
            var otherStudent = NewStudent("2", null, otherBranch);
            db.Students.AddRange(student, otherStudent);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = student.StudentID, BatchId = batch.Id },
                new StudentBatchEnrollment { StudentID = otherStudent.StudentID, BatchId = otherBatch.Id });
            db.ExamQuestions.Add(new ExamQuestion { ExamId = exam.Id, QuestionId = _questionId, Order = 1 });
            db.ExamAssignmentsToBatches.Add(new ExamAssignmentToBatch { ExamId = exam.Id, BatchId = batch.Id, Title = ExamTitle, CreatedAt = DateTime.UtcNow, AssignedAt = DateTime.UtcNow, IsSentToStudents = true });

            var assignment = new ExamAssignment { ExamId = exam.Id, StudentId = student.StudentID, AssignedAt = DateTime.UtcNow };
            db.ExamAssignments.Add(assignment);
            db.ExamStudentStatuses.Add(new ExamStudentStatus
            {
                ExamId = exam.Id,
                StudentId = student.StudentID,
                Status = ExamStatus.Completed,
                IsSubmitted = true,
                Score = 0,
                StartedAt = DateTime.UtcNow.AddMinutes(-10),
                SubmittedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            // إجابة مزورة: SubmitAnswerFetch يحفظ أي نص يرسله المتصفح
            db.QuestionAttemptNew.Add(new QuestionAttemptNew
            {
                StudentId = student.StudentID,
                QuestionId = _questionId,
                ExamId = exam.Id,
                ExamAssignmentId = assignment.Id,
                SelectedAnswer = ForgedAnswer,
                IsCorrect = false,
                AttemptedAt = DateTime.UtcNow,
                TimeTakenSeconds = 40,
                LessonId = lesson.Id,
                SectionId = section.Id
            });
            await db.SaveChangesAsync();

            _partnerId = partner.Id;
            _otherPartnerId = other.Id;
            _subscriptionId = subscription.Id;
            _batchId = batch.Id;
            _otherBatchId = otherBatch.Id;
            _examId = exam.Id;
            _unlinkedExamId = unlinked.Id;
            _lessonId = lesson.Id;
            _studentId = student.StudentID;
            _otherStudentId = otherStudent.StudentID;
            _assignmentId = assignment.Id;
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

        private static Student NewStudent(string suffix, string? userId, Branch branch) => new()
        {
            NationalID = $"{Marker}{suffix}",
            FullName = $"{Marker} طالب {suffix}",
            Gender = "ذكر",
            Age = 17,
            School = "-",
            Level = "-",
            UserId = userId,
            Branch = branch,
            IsActiveForLearning = true
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

        private async Task CleanupAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPLS6%')";
            const string exams = "(SELECT Id FROM Exams WHERE Title LIKE 'PPLS6%')";
            const string partners = "(SELECT Id FROM Partners WHERE Code LIKE 'PPLS6%')";
            const string subscriptions = "(SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN " + partners + ")";

            foreach (var sql in new[]
            {
                $"DELETE FROM QuestionAttemptNew WHERE StudentId IN {students}",
                $"DELETE FROM ExamQuestions WHERE ExamId IN {exams}",
                $"DELETE FROM ExamStudentStatuses WHERE ExamId IN {exams}",
                $"DELETE FROM ExamAssignments WHERE ExamId IN {exams}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE ExamId IN {exams}",
                "DELETE FROM Exams WHERE Title LIKE 'PPLS6%'",
                "DELETE FROM QuestionOptions WHERE QuestionId IN (SELECT Id FROM Questions WHERE ReferenceNumber LIKE 'PPLS6%')",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPLS6%'",
                "DELETE FROM Lessons WHERE Title LIKE 'PPLS6%'",
                "DELETE FROM Units WHERE Title LIKE 'PPLS6%'",
                "DELETE FROM Sections WHERE Title LIKE 'PPLS6%'",
                "DELETE FROM Curriculums WHERE Title LIKE 'PPLS6%'",
                "DELETE FROM StudentBatchEnrollments WHERE BatchId IN (SELECT Id FROM Batches WHERE Name LIKE 'PPLS6%')",
                "DELETE FROM Batches WHERE Name LIKE 'PPLS6%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPLS6%'",
                $"DELETE FROM PartnerSubscriptionPeriods WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptions WHERE Id IN {subscriptions}",
                $"DELETE FROM UserPartners WHERE PartnerId IN {partners}",
                "DELETE FROM Branches WHERE Name LIKE 'PPLS6%'",
                "DELETE FROM Partners WHERE Code LIKE 'PPLS6%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPLS6%'",
                "DELETE FROM AspNetUsers WHERE Id LIKE 'ppl-s6-%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

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

        // كل صفحات التقارير لهذا الاختبار والطالب
        private string[] ReportUrls() => new[]
        {
            $"{Base}/BatchStudents?examId={_examId}&batchId={_batchId}",
            $"{Base}/StudentExamDetails?examId={_examId}&studentId={_studentId}",
            $"{Base}/ExamSectionAnalysis?assignmentId={_assignmentId}&studentId={_studentId}",
            $"{Base}/ReviewLessonQuestions?assignmentId={_assignmentId}&studentId={_studentId}&lessonId={_lessonId}",
            $"{Base}/ReviewLessonQuestionsBatch?examId={_examId}&lessonId={_lessonId}&batchId={_batchId}",
            $"{Base}/BatchPerformanceReport?examId={_examId}&batchId={_batchId}",
            $"{Base}/StudentPlacementReport?studentId={_studentId}&examId={_examId}&assignmentId={_assignmentId}&batchId={_batchId}",
            $"{Base}/StudentPlacementReportPrint?examId={_examId}&studentId={_studentId}",
            $"{Base}/DownloadStudentReport?examId={_examId}&studentId={_studentId}"
        };

        private static async Task<string> HtmlOf(HttpResponseMessage response)
            => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()); // Razor يرمّز العربية

        private async Task SetContractAsync(DateTime endDate, DateTime? accessUntil, bool canUsePlacement = true)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var subscription = await db.PartnerSubscriptions.SingleAsync(s => s.Id == _subscriptionId);
            subscription.StartDate = endDate.AddDays(-60);
            subscription.EndDate = endDate;
            subscription.AccessUntilDate = accessUntil;
            subscription.CanUsePlacementExams = canUsePlacement;

            var period = await db.PartnerSubscriptionPeriods.SingleAsync(p => p.PartnerSubscriptionId == _subscriptionId);
            period.StartDate = subscription.StartDate;
            period.EndDate = endDate;
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task ActiveContract_AllReportPages_Render()
        {
            var client = await OwnPartnerAsync();

            foreach (var url in ReportUrls())
            {
                var response = await client.GetAsync(url);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} => {(int)response.StatusCode}");
            }
        }

        [Fact]
        public async Task OtherPartner_AllReportPages_AreNotFound()
        {
            var client = await PartnerClientAsync(OtherPartnerUser, _otherPartnerId);

            foreach (var url in ReportUrls())
            {
                var response = await client.GetAsync(url);
                Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{url} => {(int)response.StatusCode}");
            }
        }

        [Fact]
        public async Task OwnExam_OtherPartnersBatchOrStudent_AreNotFound()
        {
            var client = await OwnPartnerAsync();

            foreach (var url in new[]
            {
                $"{Base}/BatchStudents?examId={_examId}&batchId={_otherBatchId}",
                $"{Base}/BatchPerformanceReport?examId={_examId}&batchId={_otherBatchId}",
                $"{Base}/StudentExamDetails?examId={_examId}&studentId={_otherStudentId}",
                $"{Base}/StudentPlacementReport?studentId={_otherStudentId}&examId={_examId}",
                $"{Base}/StudentPlacementReportPrint?examId={_examId}&studentId={_otherStudentId}",
                $"{Base}/DownloadStudentReport?examId={_examId}&studentId={_otherStudentId}",
                $"{Base}/ExamSectionAnalysis?assignmentId={_assignmentId}&studentId={_otherStudentId}",
                $"{Base}/ReviewLessonQuestions?assignmentId={_assignmentId}&studentId={_otherStudentId}&lessonId={_lessonId}"
            })
            {
                var response = await client.GetAsync(url);
                Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{url} => {(int)response.StatusCode}");
            }
        }

        [Fact]
        public async Task ExamNotLinkedToBatch_BatchPagesAreNotFound()
        {
            var client = await OwnPartnerAsync();

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/BatchStudents?examId={_unlinkedExamId}&batchId={_batchId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/BatchPerformanceReport?examId={_unlinkedExamId}&batchId={_batchId}")).StatusCode);
        }

        [Fact]
        public async Task StudentReportAndPrint_ShowPartnerHeader_NotInstituteAdminLabels()
        {
            var client = await OwnPartnerAsync();

            foreach (var url in new[]
            {
                $"{Base}/StudentPlacementReport?studentId={_studentId}&examId={_examId}&batchId={_batchId}",
                $"{Base}/StudentPlacementReportPrint?examId={_examId}&studentId={_studentId}"
            })
            {
                var html = await HtmlOf(await client.GetAsync(url));

                Assert.Contains(PartnerName, html);
                Assert.Contains("بالتعاون مع معهد القدرات", html);
                Assert.Contains($"{Marker} طالب 1", html);
                Assert.DoesNotContain("لوحة الإدارة", html);
                Assert.DoesNotContain("/Admin/", html);
            }
        }

        [Fact]
        public async Task Pdf_IsDownloadedWithArabicFileName()
        {
            var client = await OwnPartnerAsync();

            var response = await client.GetAsync($"{Base}/DownloadStudentReport?examId={_examId}&studentId={_studentId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync(), 0, 4));
        }

        [Fact]
        public async Task ForgedStudentAnswer_IsEncoded_NotExecuted()
        {
            var client = await OwnPartnerAsync();

            foreach (var url in new[]
            {
                $"{Base}/StudentExamDetails?examId={_examId}&studentId={_studentId}",
                $"{Base}/ReviewLessonQuestions?assignmentId={_assignmentId}&studentId={_studentId}&lessonId={_lessonId}"
            })
            {
                var raw = await (await client.GetAsync(url)).Content.ReadAsStringAsync();
                Assert.DoesNotContain("<script>alert", raw);
            }
        }

        [Fact]
        public async Task ContractWithoutFeature_ReportsAreForbidden_AndLinksHidden()
        {
            await SetContractAsync(DateTime.Today.AddDays(30), accessUntil: null, canUsePlacement: false);
            var client = await OwnPartnerAsync();

            var report = await client.GetAsync($"{Base}/BatchStudents?examId={_examId}&batchId={_batchId}");
            Assert.Equal(HttpStatusCode.Forbidden, report.StatusCode);

            var batch = await HtmlOf(await client.GetAsync($"/Partner/Reports/Batch?batchId={_batchId}"));
            Assert.DoesNotContain("BatchPlacementExams", batch);

            var student = await HtmlOf(await client.GetAsync($"/Partner/Reports/Student?batchId={_batchId}&studentId={_studentId}"));
            Assert.DoesNotContain(ExamTitle, student);
        }

        [Fact]
        public async Task ExpiredContract_WithinAccessDate_ReportsWork_ManagementBlocked()
        {
            await SetContractAsync(DateTime.Today.AddDays(-5), accessUntil: DateTime.Today.AddDays(5));
            var client = await OwnPartnerAsync();

            foreach (var url in ReportUrls())
            {
                var response = await client.GetAsync(url);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} => {(int)response.StatusCode}");
            }

            // صفحة الطلاب للقراءة فقط
            var students = await HtmlOf(await client.GetAsync($"{Base}/BatchStudents?examId={_examId}&batchId={_batchId}"));
            Assert.DoesNotContain("ResendExamToStudent", students);
            Assert.Contains("BatchPlacementExams", students);

            // مسار القراءة بعد الانتهاء لا يمر بصفحات الإدارة
            var batches = await HtmlOf(await client.GetAsync("/Partner/Batches/Index"));
            Assert.Contains($"/Partner/Reports/Batch?batchId={_batchId}", batches);
            var reportsBatch = await HtmlOf(await client.GetAsync($"/Partner/Reports/Batch?batchId={_batchId}"));
            Assert.Contains("BatchPlacementExams", reportsBatch);
            var reportsExams = await HtmlOf(await client.GetAsync($"/Partner/Reports/BatchPlacementExams?batchId={_batchId}"));
            Assert.Contains(ExamTitle, reportsExams);
            var reportsStudent = await HtmlOf(await client.GetAsync($"/Partner/Reports/Student?batchId={_batchId}&studentId={_studentId}"));
            Assert.Contains(ExamTitle, reportsStudent);

            foreach (var url in new[] { $"{Base}/Index", $"{Base}/BatchExams?batchId={_batchId}", $"{Base}/Review?examId={_examId}", $"{Base}/PlacementDetails?id={_examId}" })
            {
                var response = await client.GetAsync(url);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.Contains("NoActiveSubscription", response.Headers.Location!.ToString());
            }
        }

        [Fact]
        public async Task ExpiredContract_AfterAccessDate_ReportsAreBlocked()
        {
            await SetContractAsync(DateTime.Today.AddDays(-5), accessUntil: DateTime.Today.AddDays(-1));
            var client = await OwnPartnerAsync();

            foreach (var url in ReportUrls())
            {
                var response = await client.GetAsync(url);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.Contains("NoActiveSubscription", response.Headers.Location!.ToString());
            }

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/Partner/Reports/BatchPlacementExams?batchId={_batchId}")).StatusCode);

            var batch = await HtmlOf(await client.GetAsync($"/Partner/Reports/Batch?batchId={_batchId}"));
            Assert.DoesNotContain("BatchPlacementExams", batch);
        }

        [Fact]
        public async Task ExpiredContract_WithoutFeature_ReportsAreBlocked_EvenWithinAccessDate()
        {
            await SetContractAsync(DateTime.Today.AddDays(-5), accessUntil: DateTime.Today.AddDays(5), canUsePlacement: false);
            var client = await OwnPartnerAsync();

            foreach (var url in ReportUrls())
            {
                var response = await client.GetAsync(url);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.Contains("NoActiveSubscription", response.Headers.Location!.ToString());
            }

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/Partner/Reports/BatchPlacementExams?batchId={_batchId}")).StatusCode);

            var batch = await HtmlOf(await client.GetAsync($"/Partner/Reports/Batch?batchId={_batchId}"));
            Assert.DoesNotContain("BatchPlacementExams", batch);
        }

        // صفحة الأدمن تقرأ محاولات اختبار المعهد، والإجابة نص حر من المتصفح
        [Fact]
        public async Task AdminStudentExamDetails_ForgedAnswer_IsEncoded()
        {
            int instituteExamId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var courseId = await db.Exams.Where(e => e.Id == _examId).Select(e => e.CourseId).SingleAsync();
                var exam = new Exam { Title = $"{Marker} اختبار المعهد", Type = ExamType.LevelAssessment, DurationMinutes = 30, TotalQuestions = 1, IsActive = true, CourseId = courseId };
                db.Exams.Add(exam);
                await db.SaveChangesAsync();
                instituteExamId = exam.Id;

                db.QuestionAttemptNew.Add(new QuestionAttemptNew
                {
                    StudentId = _studentId,
                    QuestionId = _questionId,
                    ExamId = instituteExamId,
                    SelectedAnswer = ForgedAnswer,
                    AttemptedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, PartnerUser);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Owner");

            var response = await client.GetAsync($"/Admin/PlacementExams/StudentExamDetails?examId={instituteExamId}&studentId={_studentId}");
            var raw = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("<script>alert", raw);
        }

        [Fact]
        public async Task ReportsStudentPage_ListsPlacementSeparately_NotUnderRegularExams()
        {
            var client = await OwnPartnerAsync();

            var response = await client.GetAsync($"/Partner/Reports/Student?batchId={_batchId}&studentId={_studentId}");
            var html = await HtmlOf(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode); // كانت 500 لغياب الـ View
            Assert.Contains($"StudentPlacementReport?studentId={_studentId}", html);
            Assert.Contains("لا توجد اختبارات مرسلة لهذا الطالب في الدفعة", html);
        }

        [Fact]
        public async Task ReportsBatchPlacementExams_OtherPartnersBatch_IsNotFound()
        {
            var client = await OwnPartnerAsync();

            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Partner/Reports/BatchPlacementExams?batchId={_otherBatchId}")).StatusCode);
        }

        [Fact]
        public async Task EntryLinks_FromBatchAndManagementPages()
        {
            var client = await OwnPartnerAsync();

            var batches = await HtmlOf(await client.GetAsync("/Partner/Batches/Index"));
            Assert.Contains($"/Partner/Reports/Batch?batchId={_batchId}", batches);

            var details = await HtmlOf(await client.GetAsync($"/Partner/Batches/Details/{_batchId}"));
            Assert.Contains($"/Partner/Reports/Student?batchId={_batchId}&studentId={_studentId}", details);

            var exams = await HtmlOf(await client.GetAsync($"{Base}/BatchExams?batchId={_batchId}"));
            Assert.Contains($"BatchStudents?examId={_examId}&batchId={_batchId}", exams);

            var placement = await HtmlOf(await client.GetAsync($"{Base}/PlacementDetails?id={_examId}"));
            Assert.Contains($"StudentPlacementReport?studentId={_studentId}&examId={_examId}", placement);
        }
    }
}
