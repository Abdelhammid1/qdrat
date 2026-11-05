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
    // PPM-S6.3 — الملكية في SentHomeworks وPartnerExam ومسودات الاختبار والواجب: شريك آخر يأخذ 404 بلا أي تغيير، والمالك يعمل كما كان.
    [Collection("SharedIntegrationDb")]
    public class PartnerOwnershipIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Marker = "PPMS6O";
        private const string OwnerPartnerUser = "ppm-s6o-partner-a";
        private const string OtherPartnerUser = "ppm-s6o-partner-b";
        private const string ForeignStudentUser = "ppm-s6o-student-b";

        private readonly CustomWebApplicationFactory _factory;
        private int _partnerA;
        private int _partnerB;
        private int _setA;
        private int _assignmentA;
        private int _studentA;
        private int _studentB;
        private int _lessonId;
        private int _examDraftA;
        private int _homeworkDraftA;
        private Guid _questionId;
        private Guid _otherQuestionId;

        public PartnerOwnershipIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Partner NewPartner(string suffix) => new()
            {
                Name = $"{Marker} شريك {suffix}",
                Code = $"{Marker}{suffix}",
                PartnershipStart = DateTime.Today.AddDays(-1),
                PartnershipEnd = DateTime.Today.AddDays(30)
            };

            Branch NewBranch(string suffix, Partner partner)
                => new() { Name = $"{Marker} فرع {suffix}", Partner = partner, Location = "-", City = "-", State = "-", Country = "-" };

            var partnerA = NewPartner("A");
            var partnerB = NewPartner("B");
            var branchA = NewBranch("A", partnerA);
            var branchB = NewBranch("B", partnerB);
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var curriculum = new Curriculum { Title = $"{Marker} منهج", Description = "-", CurriculumTypeName = "-" };
            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.AddRange(partnerA, partnerB, branchA, branchB, course, curriculum, unit);
            db.Users.AddRange(NewUser(OwnerPartnerUser), NewUser(OtherPartnerUser), NewUser("ppm-s6o-student"), NewUser(ForeignStudentUser));
            await db.SaveChangesAsync();

            db.UserPartners.AddRange(
                new UserPartner { UserId = OwnerPartnerUser, PartnerId = partnerA.Id },
                new UserPartner { UserId = OtherPartnerUser, PartnerId = partnerB.Id });

            PartnerSubscription NewContract(int partnerId) => new()
            {
                PartnerId = partnerId,
                StartDate = DateTime.Today.AddDays(-1),
                EndDate = DateTime.Today.AddDays(30),
                CanUseProfessionalModels = true,
                CanCreateHomework = true,
                CanCreateExams = true,
                SubscriptionCourses = new List<PartnerSubscriptionCourse> { new() { CourseId = course.Id, CanUsePlatformQuestionBank = true } }
            };

            var contractA = NewContract(partnerA.Id);
            var contractB = NewContract(partnerB.Id);
            db.PartnerSubscriptions.AddRange(contractA, contractB);

            var section = new Section { Title = $"{Marker} محور", CurriculumId = curriculum.Id };
            db.Sections.Add(section);
            await db.SaveChangesAsync();

            var periodA = new PartnerSubscriptionPeriod { PartnerSubscriptionId = contractA.Id, StartDate = DateTime.Today.AddDays(-1), EndDate = DateTime.Today.AddDays(30) };
            db.PartnerSubscriptionPeriods.AddRange(
                periodA,
                new PartnerSubscriptionPeriod { PartnerSubscriptionId = contractB.Id, StartDate = DateTime.Today.AddDays(-1), EndDate = DateTime.Today.AddDays(30) });

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
                IsComplete = true
            };

            var question = NewQuestion("Q1");
            var otherQuestion = NewQuestion("Q2");
            db.Questions.AddRange(question, otherQuestion);

            var batchA = new Batch { Name = $"{Marker} دفعة A", Branch = branchA, CourseId = course.Id, StartDate = DateTime.UtcNow };
            db.Batches.Add(batchA);

            Student NewStudent(string suffix, Branch branch, string? userId = null) => new()
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

            var studentA = NewStudent("A", branchA, "ppm-s6o-student");
            var studentB = NewStudent("B", branchB, ForeignStudentUser);
            db.Students.AddRange(studentA, studentB);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = studentA.StudentID, BatchId = batchA.Id });

            var set = new HomeworkSet
            {
                BatchId = batchA.Id,
                Title = $"{Marker} واجب",
                CompletionTitle = $"{Marker} واجب",
                CreatedAt = DateTime.UtcNow,
                IsExtra = true,
                IsSent = true
            };
            db.HomeworkSets.Add(set);

            var exam = new Exam { Title = $"{Marker} اختبار", Type = ExamType.Manual, CreatedAt = DateTime.UtcNow, OwnerPartnerId = partnerA.Id };
            db.Exams.Add(exam);
            await db.SaveChangesAsync();

            db.HomeworkSetStudents.Add(new HomeworkSetStudent
            {
                HomeworkSetId = set.Id,
                StudentId = studentA.StudentID,
                AssignedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                IsSubmitted = true,
                Score = 80
            });

            var assignment = new ExamAssignmentToBatch
            {
                ExamId = exam.Id,
                BatchId = batchA.Id,
                Title = exam.Title,
                CreatedAt = DateTime.UtcNow,
                AssignedAt = DateTime.UtcNow,
                IsSentToStudents = true
            };
            db.ExamAssignmentsToBatches.Add(assignment);

            var examDraft = new ExamDraft
            {
                Title = $"{Marker} مسودة اختبار",
                CurriculumId = curriculum.Id,
                PartnerId = partnerA.Id,
                CreatedAt = DateTime.UtcNow,
                DraftQuestions = new List<ExamDraftQuestion> { new() { QuestionId = question.Id } }
            };
            var homeworkDraft = new HomeworkDraft
            {
                Title = $"{Marker} مسودة واجب",
                PartnerId = partnerA.Id,
                SubscriptionPeriodId = periodA.Id,
                CourseId = course.Id,
                Questions = new List<HomeworkDraftQuestion> { new() { QuestionId = question.Id, LessonId = lesson.Id, SectionId = section.Id, Order = 1 } }
            };
            db.ExamDrafts.Add(examDraft);
            db.HomeworkDrafts.Add(homeworkDraft);
            await db.SaveChangesAsync();

            db.ExamStudentStatuses.Add(new ExamStudentStatus
            {
                ExamId = exam.Id,
                ExamAssignmentId = assignment.Id,
                StudentId = studentA.StudentID,
                Status = ExamStatus.Completed,
                IsSubmitted = true
            });
            db.QuestionAttemptNew.Add(new QuestionAttemptNew
            {
                StudentId = studentA.StudentID,
                QuestionId = question.Id,
                ExamId = exam.Id,
                ExamAssignmentId = assignment.Id,
                SelectedAnswer = "أ",
                IsCorrect = true,
                AttemptedAt = DateTime.UtcNow,
                TimeTakenSeconds = 30,
                LessonId = lesson.Id,
                SectionId = section.Id
            });
            await db.SaveChangesAsync();

            _partnerA = partnerA.Id;
            _partnerB = partnerB.Id;
            _setA = set.Id;
            _assignmentA = assignment.Id;
            _studentA = studentA.StudentID;
            _studentB = studentB.StudentID;
            _lessonId = lesson.Id;
            _examDraftA = examDraft.Id;
            _homeworkDraftA = homeworkDraft.Id;
            _questionId = question.Id;
            _otherQuestionId = otherQuestion.Id;
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

            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPMS6O%')";
            const string batches = "(SELECT Id FROM Batches WHERE Name LIKE 'PPMS6O%')";
            const string sets = "(SELECT Id FROM HomeworkSets WHERE BatchId IN " + batches + ")";
            const string partners = "(SELECT Id FROM Partners WHERE Code LIKE 'PPMS6O%')";
            const string subscriptions = "(SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN " + partners + ")";
            const string examDrafts = "(SELECT Id FROM ExamDrafts WHERE PartnerId IN " + partners + ")";
            const string homeworkDrafts = "(SELECT Id FROM HomeworkDrafts WHERE PartnerId IN " + partners + ")";

            foreach (var sql in new[]
            {
                $"DELETE FROM Notifications WHERE StudentID IN {students}",
                "DELETE FROM Notifications WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE UserName LIKE 'ppm-s6o-%')",
                $"DELETE FROM QuestionAttemptNew WHERE StudentId IN {students}",
                $"DELETE FROM ExamStudentStatuses WHERE StudentId IN {students}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE BatchId IN {batches}",
                $"DELETE FROM Exams WHERE OwnerPartnerId IN {partners}",
                $"DELETE FROM HomeworkSetAttempts WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSetStudents WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSets WHERE BatchId IN {batches}",
                $"DELETE FROM ExamDraftQuestions WHERE ExamDraftId IN {examDrafts}",
                $"DELETE FROM ExamDrafts WHERE PartnerId IN {partners}",
                $"DELETE FROM HomeworkDraftQuestions WHERE HomeworkDraftId IN {homeworkDrafts}",
                $"DELETE FROM HomeworkDrafts WHERE PartnerId IN {partners}",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPMS6O%'",
                "DELETE FROM Lessons WHERE Title LIKE 'PPMS6O%'",
                "DELETE FROM Units WHERE Title LIKE 'PPMS6O%'",
                "DELETE FROM Sections WHERE Title LIKE 'PPMS6O%'",
                "DELETE FROM Curriculums WHERE Title LIKE 'PPMS6O%'",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {batches}",
                "DELETE FROM Batches WHERE Name LIKE 'PPMS6O%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPMS6O%'",
                $"DELETE FROM PartnerSubscriptionPeriods WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptionCourses WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptions WHERE Id IN {subscriptions}",
                $"DELETE FROM UserPartners WHERE PartnerId IN {partners}",
                "DELETE FROM Branches WHERE Name LIKE 'PPMS6O%'",
                "DELETE FROM Partners WHERE Code LIKE 'PPMS6O%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPMS6O%'",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'ppm-s6o-%'"
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

            client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
            return client;
        }

        private Task<HttpClient> OwnerAsync() => PartnerClientAsync(OwnerPartnerUser, _partnerA);

        private Task<HttpClient> OtherAsync() => PartnerClientAsync(OtherPartnerUser, _partnerB);

        private async Task<HttpResponseMessage> PostAsync(HttpClient client, string userId, string path, Dictionary<string, string> fields)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(userId);
            var session = client.DefaultRequestHeaders.GetValues("Cookie").Single();

            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) };
            client.DefaultRequestHeaders.Remove("Cookie");
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

        private async Task<T> ReadAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = _factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private Dictionary<string, string> ResetFields(int studentId) => new()
        {
            ["homeworkSetId"] = _setA.ToString(),
            ["studentId"] = studentId.ToString()
        };

        private Dictionary<string, string> ResendFields(int studentId) => new()
        {
            ["examAssignmentId"] = _assignmentA.ToString(),
            ["studentId"] = studentId.ToString()
        };

        // =====================================================
        // SentHomeworks
        // =====================================================
        [Fact]
        public async Task Homework_OtherPartner_GetsNotFound_OnEveryRead()
        {
            var other = await OtherAsync();

            foreach (var url in new[]
            {
                $"/Partner/SentHomeworks/Details/{_setA}",
                $"/Partner/SentHomeworks/HomeworkReportUnified?homeworkSetId={_setA}&studentId={_studentA}",
                $"/Partner/SentHomeworks/HomeworkReportUnified?homeworkSetId={_setA}&studentId={_studentB}",
                $"/Partner/SentHomeworks/ReviewLesson?homeworkSetId={_setA}&lessonId={_lessonId}&studentId={_studentB}"
            })
            {
                Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);
            }
        }

        [Fact]
        public async Task Homework_OwnerDetails_StillWorks()
        {
            var response = await (await OwnerAsync()).GetAsync($"/Partner/SentHomeworks/Details/{_setA}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains($"{Marker} طالب A", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }

        [Fact]
        public async Task ResetHomework_OtherPartner_GetsNotFound_AndNothingChanges()
        {
            var response = await PostAsync(await OtherAsync(), OtherPartnerUser, "/Partner/SentHomeworks/ResetHomework", ResetFields(_studentA));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var row = await ReadAsync(db => db.HomeworkSetStudents.AsNoTracking().SingleAsync(x => x.HomeworkSetId == _setA));
            Assert.True(row.IsSubmitted);
            Assert.Equal(80, row.Score);
            Assert.False(await ReadAsync(db => db.HomeworkSetAttempts.AnyAsync(a => a.HomeworkSetId == _setA)));
        }

        [Fact]
        public async Task ResetHomework_Owner_NonRecipient_GetsNotFound_InsteadOfError()
        {
            var response = await PostAsync(await OwnerAsync(), OwnerPartnerUser, "/Partner/SentHomeworks/ResetHomework", ResetFields(_studentB));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ResetHomework_Owner_Works()
        {
            var response = await PostAsync(await OwnerAsync(), OwnerPartnerUser, "/Partner/SentHomeworks/ResetHomework", ResetFields(_studentA));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var row = await ReadAsync(db => db.HomeworkSetStudents.AsNoTracking().SingleAsync(x => x.HomeworkSetId == _setA));
            Assert.False(row.IsSubmitted);
            Assert.Null(row.Score);
        }

        // =====================================================
        // PartnerExam/ResendExamToStudent
        // =====================================================
        [Fact]
        public async Task ResendExam_OtherPartner_GetsNotFound_AndKeepsAttempts()
        {
            var response = await PostAsync(await OtherAsync(), OtherPartnerUser, "/Partner/PartnerExam/ResendExamToStudent", ResendFields(_studentA));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var status = await ReadAsync(db => db.ExamStudentStatuses.AsNoTracking().SingleAsync(s => s.ExamAssignmentId == _assignmentA));
            Assert.True(status.IsSubmitted);
            Assert.Equal(0, status.ResendCount);
            Assert.Equal(1, await ReadAsync(db => db.QuestionAttemptNew.CountAsync(a => a.ExamAssignmentId == _assignmentA)));
        }

        [Fact]
        public async Task ResendExam_Owner_WithForeignStudent_GetsNotFound_AndSendsNoNotification()
        {
            var response = await PostAsync(await OwnerAsync(), OwnerPartnerUser, "/Partner/PartnerExam/ResendExamToStudent", ResendFields(_studentB));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(await ReadAsync(db => db.Notifications.AnyAsync(n => n.UserId == ForeignStudentUser)));
        }

        [Fact]
        public async Task ResendExam_Owner_Works()
        {
            var response = await PostAsync(await OwnerAsync(), OwnerPartnerUser, "/Partner/PartnerExam/ResendExamToStudent", ResendFields(_studentA));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var status = await ReadAsync(db => db.ExamStudentStatuses.AsNoTracking().SingleAsync(s => s.ExamAssignmentId == _assignmentA));
            Assert.False(status.IsSubmitted);
            Assert.Equal(1, status.ResendCount);
            Assert.Equal(0, await ReadAsync(db => db.QuestionAttemptNew.CountAsync(a => a.ExamAssignmentId == _assignmentA)));
        }

        // =====================================================
        // مسودات الاختبار والواجب
        // =====================================================
        [Fact]
        public async Task Drafts_OtherPartner_GetsNotFound_OnEveryRead()
        {
            var other = await OtherAsync();

            foreach (var url in new[]
            {
                $"/Partner/PartnerExam/PreviewDraft?draftId={_examDraftA}",
                $"/Partner/PartnerExam/AddQuestion?draftId={_examDraftA}&lessonId={_lessonId}",
                $"/Partner/PartnerExam/ReplaceQuestion?draftId={_examDraftA}&oldQuestionId={_questionId}&lessonId={_lessonId}",
                $"/Partner/HomeworkDrafts/Preview/{_homeworkDraftA}",
                $"/Partner/HomeworkDrafts/AddQuestion?draftId={_homeworkDraftA}&lessonId={_lessonId}",
                $"/Partner/HomeworkDrafts/ReplaceQuestion?draftId={_homeworkDraftA}&oldQuestionId={_questionId}&lessonId={_lessonId}"
            })
            {
                Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);
            }
        }

        [Fact]
        public async Task Drafts_OtherPartner_CannotChangeQuestions()
        {
            var other = await OtherAsync();

            var writes = new (string Path, Dictionary<string, string> Fields)[]
            {
                ("/Partner/PartnerExam/ConfirmReplaceQuestion", new()
                {
                    ["draftId"] = _examDraftA.ToString(), ["oldQuestionId"] = _questionId.ToString(), ["newQuestionId"] = _otherQuestionId.ToString()
                }),
                ("/Partner/PartnerExam/ConfirmAddQuestion", new()
                {
                    ["draftId"] = _examDraftA.ToString(), ["questionId"] = _otherQuestionId.ToString()
                }),
                ("/Partner/HomeworkDrafts/ConfirmReplaceQuestion", new()
                {
                    ["DraftId"] = _homeworkDraftA.ToString(), ["OldQuestionId"] = _questionId.ToString(), ["NewQuestionId"] = _otherQuestionId.ToString()
                }),
                ("/Partner/HomeworkDrafts/ConfirmAddQuestion", new()
                {
                    ["draftId"] = _homeworkDraftA.ToString(), ["questionId"] = _otherQuestionId.ToString()
                })
            };

            foreach (var (path, fields) in writes)
            {
                Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(other, OtherPartnerUser, path, fields)).StatusCode);
            }

            var examIds = await ReadAsync(db => db.ExamDraftQuestions.AsNoTracking().Where(q => q.ExamDraftId == _examDraftA).Select(q => q.QuestionId).ToListAsync());
            var homeworkIds = await ReadAsync(db => db.HomeworkDraftQuestions.AsNoTracking().Where(q => q.HomeworkDraftId == _homeworkDraftA).Select(q => q.QuestionId).ToListAsync());
            Assert.Equal(_questionId, Assert.Single(examIds));
            Assert.Equal(_questionId, Assert.Single(homeworkIds));
        }

        [Fact]
        public async Task Drafts_Owner_StillPreviews()
        {
            var owner = await OwnerAsync();

            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/Partner/PartnerExam/PreviewDraft?draftId={_examDraftA}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/Partner/HomeworkDrafts/Preview/{_homeworkDraftA}")).StatusCode);
        }
    }
}
