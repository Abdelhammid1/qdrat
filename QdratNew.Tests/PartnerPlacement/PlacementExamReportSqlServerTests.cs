using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PuppeteerSharp;
using QdratNew.Data;
using QdratNew.Services.Exams.Placement;
using QdratNew.Services.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Reports;
using QdratNew.Tests.Integration;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Reports;
using Xunit;
using static QdratNew.Tests.PartnerPlacement.PlacementReportFixture;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S3.3 — استعلامات التقارير على SQL Server حقيقي بإعداد الإنتاج (UseCompatibilityLevel(120)).
    // InMemory لا يكشف أخطاء الترجمة. التشغيل: QDRAT_TEST_SQL على قاعدة الاختبار المعزولة؛ بدونه تُتخطّى.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class PlacementExamReportSqlServerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly CustomWebApplicationFactory _factory;

        public PlacementExamReportSqlServerTests(CustomWebApplicationFactory factory) => _factory = factory;

        public Task InitializeAsync() => RtkRealDb.IsConfigured ? CleanupAsync() : Task.CompletedTask;

        public Task DisposeAsync() => RtkRealDb.IsConfigured ? CleanupAsync() : Task.CompletedTask;

        private static async Task CleanupAsync()
        {
            await using var db = RtkRealDb.CreateDb();

            const string exams = "(SELECT Id FROM Exams WHERE Title LIKE 'PPLS3%')";
            const string batches = "(SELECT Id FROM Batches WHERE Name LIKE 'PPLS3%')";
            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPLS3%')";
            const string questions = "(SELECT Id FROM Questions WHERE ReferenceNumber LIKE 'PPLS3%')";

            foreach (var sql in new[]
            {
                $"DELETE FROM IntegrityViolationLogs WHERE StudentId IN {students}",
                $"DELETE FROM QuestionAttemptNew WHERE ExamId IN {exams}",
                $"DELETE FROM ExamStudentStatuses WHERE ExamId IN {exams}",
                $"DELETE FROM ExamAssignments WHERE ExamId IN {exams}",
                $"DELETE FROM ExamQuestions WHERE ExamId IN {exams}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE ExamId IN {exams}",
                "DELETE FROM Exams WHERE Title LIKE 'PPLS3%'",
                $"DELETE FROM QuestionOptions WHERE QuestionId IN {questions}",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPLS3%'",
                "DELETE FROM VerbalPassages WHERE Title LIKE 'PPLS3%'",
                "DELETE FROM Lessons WHERE Title LIKE 'PPLS3%'",
                "DELETE FROM Units WHERE Title LIKE 'PPLS3%'",
                "DELETE FROM Sections WHERE Title LIKE 'PPLS3%'",
                "DELETE FROM Curriculums WHERE Title LIKE 'PPLS3%'",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {batches}",
                "DELETE FROM Batches WHERE Name LIKE 'PPLS3%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPLS3%'",
                "DELETE FROM Parents WHERE NationalID LIKE 'PPLS3%'",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'ppls3-%'",
                "DELETE FROM Branches WHERE Name LIKE 'PPLS3%'",
                "DELETE FROM Partners WHERE Code LIKE 'PPLS3%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPLS3%'"
            })
            {
                // مجموعة SharedIntegrationDb تعمل بالتوازي على نفس القاعدة؛ قد يختار SQL Server هذا الحذف ضحية Deadlock
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        await db.Database.ExecuteSqlRawAsync(sql);
                        break;
                    }
                    catch (SqlException ex) when (ex.Number == 1205 && attempt < 5)
                    {
                        await Task.Delay(200 * attempt);
                    }
                }
            }
        }

        private static async Task<PlacementReportSeed> SeedOnSqlAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            return await SeedAsync(db);
        }

        [SqlServerFact]
        public async Task BatchStudents_Institute_MatchesExpectedOnSqlServer()
        {
            var seed = await SeedOnSqlAsync();
            await using var db = RtkRealDb.CreateDb();

            var view = Assert.IsType<ViewResult>(
                await PlacementReportHarness.NewAdminController(db).BatchStudents(seed.InstituteExam, seed.InstituteBatch));
            var students = Assert.IsType<List<PlacementExamStudentVm>>(view.Model);

            Assert.Equal(4, students.Count);
            Assert.Equal($"{Marker} المعهد 1", view.ViewData["ExamTitle"]);

            var full = students.Single(x => x.StudentId == seed.FullStudent);
            Assert.True(full.IsSubmitted);
            Assert.Equal(seed.FullAssignment, full.AssignmentId);
            Assert.Equal(50, full.Score);

            var partial = students.Single(x => x.StudentId == seed.PartialStudent);
            Assert.False(partial.IsSubmitted);
            Assert.True(partial.IsIntegrityBlocked);

            // لم يحلّ هذا الاختبار: يظهر عبر fallback اختبار آخر في نفس الدفعة
            var other = students.Single(x => x.StudentId == seed.OtherExamStudent);
            Assert.Equal(seed.OtherExamAssignment, other.AssignmentId);
            Assert.Equal(50, other.Score);

            var noAssignment = students.Single(x => x.StudentId == seed.NoAssignmentStudent);
            Assert.Equal(0, noAssignment.AssignmentId);
            Assert.Equal(100, noAssignment.Score);
        }

        [SqlServerFact]
        public async Task BatchPerformance_MatchesExpectedOnSqlServer()
        {
            var seed = await SeedOnSqlAsync();
            await using var db = RtkRealDb.CreateDb();

            var view = Assert.IsType<ViewResult>(
                await PlacementReportHarness.NewAdminController(db).BatchPerformanceReport(seed.InstituteExam, seed.InstituteBatch));
            var model = Assert.IsType<AdminExamSectionAnalysisViewModel>(view.Model);

            var quant = model.SectionReports.Single(x => x.SectionTitle.Contains("الجبر"));
            Assert.Equal(2, quant.Correct);
            Assert.Equal(3, quant.Wrong);
            Assert.Equal(40, quant.Score);
        }

        [SqlServerFact]
        public async Task ReviewBatch_MatchesExpectedOnSqlServer()
        {
            var seed = await SeedOnSqlAsync();
            await using var db = RtkRealDb.CreateDb();

            var view = Assert.IsType<ViewResult>(
                await PlacementReportHarness.NewAdminController(db).ReviewLessonQuestionsBatch(seed.InstituteExam, seed.Lesson1));
            var model = Assert.IsType<BatchLessonQuestionsReportViewModel>(view.Model);

            Assert.Equal(2, model.TotalQuestions);
            var q1 = model.Questions.Single(x => x.QuestionId == Q1);
            Assert.Equal(3, q1.TotalAnswers);
            Assert.Equal(2, q1.CorrectAnswers);
            Assert.Equal(1, q1.SkippedCount); // 4 طلاب في الدفعة
            Assert.Equal(2, q1.Options.Count);
            Assert.Equal(2, q1.Options.Single(o => o.IsCorrect).SelectedCount);
        }

        [SqlServerFact]
        public async Task SectionAnalysis_And_ReviewLesson_MatchExpectedOnSqlServer()
        {
            var seed = await SeedOnSqlAsync();
            await using var db = RtkRealDb.CreateDb();
            var controller = PlacementReportHarness.NewAdminController(db);

            var analysis = Assert.IsType<AdminExamSectionAnalysisViewModel>(
                Assert.IsType<ViewResult>(await controller.ExamSectionAnalysis(seed.FullAssignment, seed.FullStudent)).Model);
            var quant = analysis.SectionReports.Single(x => x.SectionTitle.Contains("الجبر"));
            Assert.Equal(1, quant.Correct);
            Assert.Equal(2, quant.Wrong);
            Assert.Equal(seed.Lesson1, quant.LessonBreakdown.Single(l => l.LessonTitle.Contains("المعادلات")).LessonId);

            var review = Assert.IsType<ExamReviewViewModel>(
                Assert.IsType<ViewResult>(await controller.ReviewLessonQuestions(seed.FullAssignment, seed.FullStudent, seed.Lesson1)).Model);
            Assert.Equal(2, review.TotalQuestions);
            Assert.All(review.Questions, q => Assert.Equal(new[] { "الخيار أ", "الخيار ب" }, q.Options.Select(o => o.Text)));
        }

        [SqlServerFact]
        public async Task Report_AllAssignmentPaths_MatchExpectedOnSqlServer()
        {
            var seed = await SeedOnSqlAsync();
            await using var db = RtkRealDb.CreateDb();
            var controller = PlacementReportHarness.NewAdminController(db);

            var byAssignment = Assert.IsType<PlacementReportViewModel>(
                Assert.IsType<ViewResult>(await controller.AdminPlacementReport(seed.FullStudent, seed.FullAssignment)).Model);
            Assert.Equal(seed.FullAssignment, byAssignment.AssignmentId);
            Assert.Equal(2, byAssignment.CorrectAnswers);
            Assert.Equal(1, byAssignment.DontKnowAnswers);
            Assert.Equal(20, byAssignment.SolveMinutes);
            Assert.Equal("0500000000", byAssignment.Student.ParentPhone);

            var byBatch = Assert.IsType<PlacementReportViewModel>(
                Assert.IsType<ViewResult>(await controller.AdminPlacementReport(seed.FullStudent, 0, seed.InstituteExam, seed.InstituteBatch)).Model);
            Assert.Equal(seed.FullAssignment, byBatch.AssignmentId);

            var noRow = Assert.IsType<PlacementReportViewModel>(
                Assert.IsType<ViewResult>(await controller.AdminPlacementReport(seed.NoAssignmentStudent, 0, seed.InstituteExam)).Model);
            Assert.Equal(0, noRow.AssignmentId);
            Assert.Equal(1, noRow.CorrectAnswers);

            var print = Assert.IsType<AdminPlacementReportViewModel>(
                Assert.IsType<ViewResult>(await controller.AdminPlacementReportPrint(seed.InstituteExam, seed.FullStudent)).Model);
            Assert.Equal(50, print.OverallPercent);
            Assert.Equal(20, print.AverageSolveMinutes);

            var details = Assert.IsType<StudentExamDetailsVm>(
                Assert.IsType<ViewResult>(await controller.StudentExamDetails(seed.InstituteExam, seed.FullStudent)).Model);
            Assert.Equal(2, details.CorrectCount);
            Assert.Equal(2, details.WrongCount);
        }

        // =========================================
        // نطاق الشريك: استعلامات مختلفة عن الأدمن (جوينات Branch) تُترجم فعليًا بتوافق 2014
        // =========================================
        [SqlServerFact]
        public async Task PartnerScope_AllReports_TranslateAndIsolateOnSqlServer()
        {
            var s = await SeedOnSqlAsync();
            await using var db = RtkRealDb.CreateDb();
            var service = new PlacementExamReportService(
                db,
                new PlacementExamScopeGuard(),
                new Mock<ITimeZoneService>().Object,
                new ExamRecommendationService(),
                new ReportBrowserProvider(new Mock<IBrowserFetcher>().Object),
                new Mock<IWebHostEnvironment>().Object,
                NullLogger<PlacementExamReportService>.Instance);
            var scope = PlacementScope.Partner(s.PartnerA, "a");

            var students = await service.GetBatchStudentsAsync(scope, s.ExamOfA, s.BatchA);
            Assert.Equal(3, students!.Students.Count);
            Assert.DoesNotContain(students.Students, x => x.StudentId == s.ForeignInBatchA);

            var performance = await service.GetBatchPerformanceReportAsync(scope, s.ExamOfA, s.BatchA);
            var quant = performance.Model!.SectionReports.Single(x => x.SectionTitle.Contains("الجبر"));
            Assert.Equal(3, quant.Correct);
            Assert.Equal(1, quant.Wrong);

            var reviewBatch = await service.GetReviewLessonQuestionsBatchAsync(scope, s.ExamOfA, s.Lesson1);
            Assert.Equal(0, reviewBatch!.Questions.Single(x => x.QuestionId == Q1).SkippedCount);
            Assert.Null(await service.GetReviewLessonQuestionsBatchAsync(scope, s.ExamOfA, s.LessonNotInExams));

            Assert.Equal(PlacementReportOutcome.Success, (await service.GetSectionAnalysisAsync(scope, s.ActiveAAssignment, s.ActiveA)).Outcome);
            Assert.Equal(PlacementReportOutcome.Success, (await service.GetReviewLessonQuestionsAsync(scope, s.ActiveAAssignment, s.ActiveA, s.Lesson1)).Outcome);
            Assert.Equal(PlacementReportOutcome.Success, (await service.GetStudentPlacementReportPrintAsync(scope, s.ExamOfA, s.ActiveA)).Outcome);
            Assert.NotNull(await service.GetStudentExamDetailsAsync(scope, s.ExamOfA, s.ArchivedA));

            var byBatch = await service.GetStudentPlacementReportAsync(scope, s.ActiveA, 0, s.ExamOfA, s.BatchA);
            Assert.Equal(s.ActiveAAssignment, byBatch.Model!.AssignmentId);

            var derivedBatch = await service.GetStudentPlacementReportAsync(scope, s.ActiveA, 0, s.ExamOfA, 0);
            Assert.Equal(s.ActiveAAssignment, derivedBatch.Model!.AssignmentId);

            // العزل
            Assert.Null(await service.GetBatchStudentsAsync(scope, s.ExamOfB, s.BatchA));
            Assert.Null(await service.GetBatchStudentsAsync(scope, s.ExamOfA, s.BatchB));
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetSectionAnalysisAsync(scope, s.StudentBAssignment, s.StudentB)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetStudentPlacementReportAsync(scope, s.ForeignInBatchA, 0, s.ExamOfA, s.BatchA)).Outcome);
        }

        [SqlServerFact]
        public async Task AdminScope_BatchFallback_DoesNotBorrowPartnerExamOnSqlServer()
        {
            var s = await SeedOnSqlAsync();
            await using var db = RtkRealDb.CreateDb();

            var view = Assert.IsType<ViewResult>(
                await PlacementReportHarness.NewAdminController(db).BatchStudents(s.InstituteExamOnPartnerBatch, s.BatchA));
            var onlyPartner = Assert.IsType<List<PlacementExamStudentVm>>(view.Model).Single(x => x.StudentId == s.OnlyPartnerExamA);

            Assert.Equal(0, onlyPartner.AssignmentId);
            Assert.Equal(0, onlyPartner.Score);
        }

        // =========================================
        // HTTP حقيقي: الـ Views تُعرض بنفس الموديلات، واختبار الشريك 404
        // =========================================
        private HttpClient OwnerClient()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "ppl-s3-owner");
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Owner");
            return client;
        }

        private static async Task<string> GetOkHtmlAsync(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} => {(int)response.StatusCode}");
            return html;
        }

        [SqlServerFact]
        public async Task AdminReportPages_RenderRealViews()
        {
            var s = await SeedOnSqlAsync();
            var client = OwnerClient();
            const string root = "/Admin/PlacementExams";

            Assert.Contains("طالب full", await GetOkHtmlAsync(client, $"{root}/BatchStudents?examId={s.InstituteExam}&batchId={s.InstituteBatch}"));
            Assert.Contains("محور الجبر", await GetOkHtmlAsync(client, $"{root}/BatchPerformanceReport?examId={s.InstituteExam}&batchId={s.InstituteBatch}"));
            Assert.Contains("محور الجبر", await GetOkHtmlAsync(client, $"{root}/ExamSectionAnalysis?assignmentId={s.FullAssignment}&studentId={s.FullStudent}"));
            Assert.Contains("مؤشر المعادلات", await GetOkHtmlAsync(client, $"{root}/ReviewLessonQuestions?assignmentId={s.FullAssignment}&studentId={s.FullStudent}&lessonId={s.Lesson1}"));
            Assert.Contains("سؤال 1", await GetOkHtmlAsync(client, $"{root}/ReviewLessonQuestionsBatch?examId={s.InstituteExam}&lessonId={s.Lesson1}"));
            Assert.Contains("طالب full", await GetOkHtmlAsync(client, $"{root}/StudentExamDetails?examId={s.InstituteExam}&studentId={s.FullStudent}"));
            Assert.Contains("طالب full", await GetOkHtmlAsync(client, $"{root}/AdminPlacementReport?studentId={s.FullStudent}&assignmentId={s.FullAssignment}"));
            Assert.Contains("تقرير أداء الطالب", await GetOkHtmlAsync(client, $"{root}/AdminPlacementReportPrint?examId={s.InstituteExam}&studentId={s.FullStudent}"));
        }

        [SqlServerFact]
        public async Task AdminDownloadStudentReport_ReturnsPdfOverHttp()
        {
            var s = await SeedOnSqlAsync();

            var response = await OwnerClient().GetAsync($"/Admin/PlacementExams/DownloadStudentReport?examId={s.InstituteExam}&studentId={s.FullStudent}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await response.Content.ReadAsByteArrayAsync()).Take(4).ToArray()));
        }

        [SqlServerFact]
        public async Task AdminReportPages_PartnerExam_IsNotFoundOverHttp()
        {
            var s = await SeedOnSqlAsync();
            var client = OwnerClient();
            const string root = "/Admin/PlacementExams";

            foreach (var url in new[]
            {
                $"{root}/BatchStudents?examId={s.ExamOfA}&batchId={s.BatchA}",
                $"{root}/ExamSectionAnalysis?assignmentId={s.ActiveAAssignment}&studentId={s.ActiveA}",
                $"{root}/ReviewLessonQuestions?assignmentId={s.ActiveAAssignment}&studentId={s.ActiveA}&lessonId={s.Lesson1}",
                $"{root}/ReviewLessonQuestionsBatch?examId={s.ExamOfA}&lessonId={s.Lesson1}",
                $"{root}/StudentExamDetails?examId={s.ExamOfA}&studentId={s.ActiveA}",
                $"{root}/AdminPlacementReport?studentId={s.ActiveA}&assignmentId={s.ActiveAAssignment}",
                $"{root}/AdminPlacementReportPrint?examId={s.ExamOfA}&studentId={s.ActiveA}",
                $"{root}/DownloadStudentReport?examId={s.ExamOfA}&studentId={s.ActiveA}",
                $"{root}/DownloadReportChrome?examId={s.ExamOfA}&studentId={s.ActiveA}"
            })
            {
                var response = await client.GetAsync(url);
                Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{url} => {(int)response.StatusCode}");
            }

            // هذا الـ Action يعرض رسالة بدل 404 منذ البداية؛ المهم ألا يعرض بيانات الاختبار
            var performance = await GetOkHtmlAsync(client, $"{root}/BatchPerformanceReport?examId={s.ExamOfA}&batchId={s.BatchA}");
            Assert.Contains("لم يتم العثور", performance);
            Assert.DoesNotContain("محور الجبر", performance);
        }
    }
}
