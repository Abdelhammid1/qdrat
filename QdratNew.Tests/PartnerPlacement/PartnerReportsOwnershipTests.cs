using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Reports.Implementations;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S1 — عزل تقارير الشريك: لا يقرأ شريك دفعات أو طلاب أو واجبات أو اختبارات شريك آخر.
    public class PartnerReportsOwnershipTests
    {
        private const int PartnerA = 1;
        private const int PartnerB = 2;

        private const int BatchA = 10;
        private const int BatchB = 20;
        private const int StudentA = 100;
        private const int StudentB = 200;
        private const int HomeworkB = 2000;
        private const int ExamB = 3000;
        private const int HomeworkA = 1000;
        private const int ExamA = 4000;

        private static ApplicationDbContext SeedTwoPartners()
        {
            var db = TestDbContextFactory.CreateFreshContext(out _);

            db.Branches.AddRange(
                new Branch { Id = 1, PartnerId = PartnerA, Name = "A", Location = "-", City = "-", State = "-", Country = "-" },
                new Branch { Id = 2, PartnerId = PartnerB, Name = "B", Location = "-", City = "-", State = "-", Country = "-" });

            db.Batches.AddRange(
                new Batch { Id = BatchA, Name = "دفعة A", BranchId = 1, CourseId = 1, StartDate = DateTime.Today },
                new Batch { Id = BatchB, Name = "دفعة B", BranchId = 2, CourseId = 1, StartDate = DateTime.Today });

            db.Students.AddRange(NewStudent(StudentA, 1), NewStudent(StudentB, 2));

            db.HomeworkSets.AddRange(
                new HomeworkSet { Id = HomeworkA, Title = "واجب A", BatchId = BatchA, IsSent = true, CompletionTitle = "-" },
                new HomeworkSet { Id = HomeworkB, Title = "واجب B", BatchId = BatchB, IsSent = true, CompletionTitle = "-" });
            db.HomeworkSetStudents.AddRange(
                new HomeworkSetStudent { HomeworkSetId = HomeworkA, StudentId = StudentA },
                new HomeworkSetStudent { HomeworkSetId = HomeworkB, StudentId = StudentB });

            db.ExamAssignmentsToBatches.AddRange(
                new ExamAssignmentToBatch { Id = ExamA, Title = "اختبار A", BatchId = BatchA, IsSentToStudents = true },
                new ExamAssignmentToBatch { Id = ExamB, Title = "اختبار B", BatchId = BatchB, IsSentToStudents = true });
            db.ExamStudentStatuses.AddRange(
                new ExamStudentStatus { ExamAssignmentId = ExamA, StudentId = StudentA, ExamId = 1 },
                new ExamStudentStatus { ExamAssignmentId = ExamB, StudentId = StudentB, ExamId = 1 });

            db.SaveChanges();
            return db;
        }

        private static Student NewStudent(int id, int branchId) => new()
        {
            StudentID = id,
            BranchId = branchId,
            FullName = "طالب " + id,
            Gender = "ذكر",
            School = "-",
            NationalID = id.ToString(),
            Email = id + "@test.local",
            PhoneNumber = "0500000000",
            UserId = "u" + id,
            EnrollmentStatus = "نشط"
        };

        [Fact]
        public void BatchReports_OtherPartnersBatch_ReturnNull()
        {
            using var db = SeedTwoPartners();
            var service = new BatchReportService(db);

            Assert.Null(service.GetBatchReport(PartnerA, BatchB));
            Assert.Null(service.GetBatchHomeworkReport(PartnerA, BatchB));
            Assert.Null(service.GetBatchExamReport(PartnerA, BatchB));
        }

        [Fact]
        public void BatchReports_OwnBatch_ReturnData()
        {
            using var db = SeedTwoPartners();
            var service = new BatchReportService(db);

            Assert.Equal(BatchA, service.GetBatchReport(PartnerA, BatchA).BatchId);
            Assert.Single(service.GetBatchHomeworkReport(PartnerA, BatchA).Homeworks);
            Assert.Single(service.GetBatchExamReport(PartnerA, BatchA).Exams);
        }

        [Fact]
        public void StudentReport_OtherPartnersBatch_ReturnsNull()
        {
            using var db = SeedTwoPartners();

            Assert.Null(new StudentBatchReportService(db).GetStudentReport(PartnerA, BatchB, StudentB));
        }

        [Fact]
        public void StudentReport_OwnBatchWithOtherPartnersStudent_DoesNotExposeTheStudent()
        {
            using var db = SeedTwoPartners();

            Assert.Null(new StudentBatchReportService(db).GetStudentReport(PartnerA, BatchA, StudentB));
        }

        [Fact]
        public void StudentReport_StudentFromAnotherBranchEnrolledInOwnBatch_IsAllowed()
        {
            using var db = SeedTwoPartners();
            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = StudentB, BatchId = BatchA });
            db.SaveChanges();

            var report = new StudentBatchReportService(db).GetStudentReport(PartnerA, BatchA, StudentB);

            Assert.Equal(StudentB, report.StudentId);
        }

        [Fact]
        public void StudentReport_OwnStudent_ReturnsData()
        {
            using var db = SeedTwoPartners();

            var report = new StudentBatchReportService(db).GetStudentReport(PartnerA, BatchA, StudentA);

            Assert.Equal(StudentA, report.StudentId);
            Assert.Single(report.Homeworks);
            Assert.Single(report.Exams);
        }

        [Fact]
        public void HomeworkAndExamReports_OtherPartnersRecords_ReturnNull()
        {
            using var db = SeedTwoPartners();
            var service = new StudentBatchReportService(db);

            Assert.Null(service.GetHomeworkStudents(PartnerA, HomeworkB));
            Assert.Null(service.GetExamStudents(PartnerA, ExamB));
            Assert.Null(service.GetStudentHomeworkDetails(PartnerA, HomeworkB, StudentB));
            Assert.Null(service.GetStudentExamDetails(PartnerA, ExamB, StudentB));
        }

        [Fact]
        public void HomeworkAndExamReports_OwnRecords_ReturnData()
        {
            using var db = SeedTwoPartners();
            var service = new StudentBatchReportService(db);

            Assert.Single(service.GetHomeworkStudents(PartnerA, HomeworkA).Students);
            Assert.Single(service.GetExamStudents(PartnerA, ExamA).Students);
            Assert.Equal(StudentA, service.GetStudentHomeworkDetails(PartnerA, HomeworkA, StudentA).StudentId);
            Assert.Equal(StudentA, service.GetStudentExamDetails(PartnerA, ExamA, StudentA).StudentId);
        }
    }

    // المسار الكامل عبر Program.cs الحقيقي على قاعدة الاختبار المعزولة.
    [Collection("SharedIntegrationDb")]
    public class PartnerReportsOwnershipIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string CodeA = "PPL-RPT-A";
        private const string CodeB = "PPL-RPT-B";
        private const string CourseName = "دورة اختبار تقارير PPL";
        private const string UserName = "ppl-rpt-partner-a";

        private readonly CustomWebApplicationFactory _factory;
        private string _userId = string.Empty;
        private int _partnerA;
        private int _batchA;
        private int _batchB;

        public PartnerReportsOwnershipIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var user = new ApplicationUser { UserName = UserName, Email = UserName + "@test.local", NationalID = "1000000078" };
            var created = await users.CreateAsync(user, "P@ssw0rd123!");
            if (!created.Succeeded)
                throw new InvalidOperationException(string.Join(", ", created.Errors.Select(e => e.Description)));
            _userId = user.Id;

            var partnerA = NewPartner(CodeA);
            var partnerB = NewPartner(CodeB);
            db.Partners.AddRange(partnerA, partnerB);
            var course = new Course { Name = CourseName, Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            _partnerA = partnerA.Id;

            var branchA = NewBranch(partnerA.Id, "فرع تقارير A");
            var branchB = NewBranch(partnerB.Id, "فرع تقارير B");
            db.Branches.AddRange(branchA, branchB);
            db.UserPartners.Add(new UserPartner { UserId = _userId, PartnerId = partnerA.Id });
            await db.SaveChangesAsync();

            var batchA = new Batch { Name = "دفعة تقارير A", StartDate = DateTime.UtcNow, CourseId = course.Id, BranchId = branchA.Id };
            var batchB = new Batch { Name = "دفعة تقارير B", StartDate = DateTime.UtcNow, CourseId = course.Id, BranchId = branchB.Id };
            db.Batches.AddRange(batchA, batchB);
            await db.SaveChangesAsync();
            _batchA = batchA.Id;
            _batchB = batchB.Id;
        }

        public Task DisposeAsync() => CleanupAsync();

        private static Partner NewPartner(string code) => new()
        {
            Name = "شريك " + code,
            Code = code,
            PartnershipStart = DateTime.Today.AddDays(-10),
            PartnershipEnd = DateTime.Today.AddDays(10)
        };

        private static Branch NewBranch(int partnerId, string name) =>
            new() { PartnerId = partnerId, Name = name, Location = "-", City = "-", State = "-", Country = "-" };

        private async Task CleanupAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var partnerIds = $"(SELECT Id FROM Partners WHERE Code IN ('{CodeA}','{CodeB}'))";
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM Batches WHERE BranchId IN (SELECT Id FROM Branches WHERE PartnerId IN {partnerIds})");
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM UserPartners WHERE PartnerId IN {partnerIds}");
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM Branches WHERE PartnerId IN {partnerIds}");
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM Partners WHERE Code IN ('{CodeA}','{CodeB}')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Courses WHERE Name = {0}", CourseName);
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM AspNetUsers WHERE UserName = '{UserName}'");
        }

        private HttpClient CreateClient(string userId)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Partner");
            return client;
        }

        private async Task<string> SelectPartnerAAndGetSessionCookieAsync(HttpClient client)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(_userId);
            var request = new HttpRequestMessage(HttpMethod.Post, "/Partner/PartnerContext/Set")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["partnerId"] = _partnerA.ToString(),
                    ["__RequestVerificationToken"] = token
                })
            };
            request.Headers.Add("Cookie", cookie);

            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return response.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        }

        private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string? cookie)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (cookie != null)
                request.Headers.Add("Cookie", cookie);
            return await client.SendAsync(request);
        }

        [Fact]
        public async Task BatchReport_OwnBatch_ReturnsOk()
        {
            var client = CreateClient(_userId);
            var cookie = await SelectPartnerAAndGetSessionCookieAsync(client);

            var response = await GetAsync(client, $"/Partner/Reports/Batch?batchId={_batchA}", cookie);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task BatchReport_OtherPartnersBatch_ReturnsNotFound()
        {
            var client = CreateClient(_userId);
            var cookie = await SelectPartnerAAndGetSessionCookieAsync(client);

            var response = await GetAsync(client, $"/Partner/Reports/Batch?batchId={_batchB}", cookie);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // يتأكد أن كل استعلامات الملكية تُترجم فعلًا على SQL Server (InMemory لا يكشف ذلك)
        [Fact]
        public async Task AllReportQueries_TranslateOnSqlServer_AndHideOtherPartnersData()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var batches = new BatchReportService(db);
            var students = new StudentBatchReportService(db);

            Assert.Null(batches.GetBatchHomeworkReport(_partnerA, _batchB));
            Assert.Null(batches.GetBatchExamReport(_partnerA, _batchB));
            Assert.NotNull(batches.GetBatchHomeworkReport(_partnerA, _batchA));
            Assert.NotNull(batches.GetBatchExamReport(_partnerA, _batchA));

            Assert.Null(students.GetStudentReport(_partnerA, _batchB, 1));
            Assert.Null(students.GetStudentReport(_partnerA, _batchA, -1));
            Assert.Null(students.GetHomeworkStudents(_partnerA, -1));
            Assert.Null(students.GetExamStudents(_partnerA, -1));
            Assert.Null(students.GetStudentHomeworkDetails(_partnerA, -1, -1));
            Assert.Null(students.GetStudentExamDetails(_partnerA, -1, -1));

            await Task.CompletedTask;
        }

        [Fact]
        public async Task Reports_WithoutActivePartner_RedirectToSelect()
        {
            var response = await GetAsync(CreateClient(_userId), $"/Partner/Reports/Batch?batchId={_batchA}", null);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Partner/PartnerContext/Select", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task Reports_SessionBelongsToAnotherUser_RedirectToSelect()
        {
            var owner = CreateClient(_userId);
            var cookie = await SelectPartnerAAndGetSessionCookieAsync(owner);

            var response = await GetAsync(CreateClient("ppl-rpt-outsider"), $"/Partner/Reports/Batch?batchId={_batchA}", cookie);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Partner/PartnerContext/Select", response.Headers.Location!.ToString());
        }
    }
}
