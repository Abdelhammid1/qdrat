using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PuppeteerSharp;
using QdratNew.Data;
using QdratNew.Services.Exams.Placement;
using QdratNew.Services.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Reports;
using Xunit;
using static QdratNew.Tests.PartnerPlacement.PlacementReportFixture;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S3.3 — عزل النطاق في خدمة التقارير: ما هو خارج النطاق يعامَل كغير موجود.
    public class PlacementExamReportServiceTests
    {
        private static async Task<(PlacementExamReportService Service, PlacementReportSeed Seed, PlacementScope ScopeA, PlacementScope ScopeB, Mock<IBrowserFetcher> Fetcher)> NewAsync()
        {
            ApplicationDbContext db = TestDbContextFactory.CreateFreshContext(out _);
            var seed = await SeedAsync(db);
            var fetcher = new Mock<IBrowserFetcher>();

            var service = new PlacementExamReportService(
                db,
                new PlacementExamScopeGuard(),
                new Mock<ITimeZoneService>().Object,
                new ExamRecommendationService(),
                new ReportBrowserProvider(fetcher.Object),
                new Mock<IWebHostEnvironment>().Object,
                NullLogger<PlacementExamReportService>.Instance);

            return (service, seed, PlacementScope.Partner(seed.PartnerA, "a"), PlacementScope.Partner(seed.PartnerB, "b"), fetcher);
        }

        private static readonly PlacementScope Admin = PlacementScope.Admin("admin");

        // =========================================
        // الأدمن لا يرى نتائج اختبارات الشركاء
        // =========================================
        [Fact]
        public async Task Admin_AllReports_TreatPartnerExamAsMissing()
        {
            var (service, s, _, _, _) = await NewAsync();

            Assert.Null(await service.GetBatchStudentsAsync(Admin, s.ExamOfA, s.BatchA));
            Assert.Null(await service.GetStudentExamDetailsAsync(Admin, s.ExamOfA, s.ActiveA));
            Assert.Null(await service.GetReviewLessonQuestionsBatchAsync(Admin, s.ExamOfA, s.Lesson1));
            Assert.Null(await service.BuildStudentReportPdfAsync(Admin, s.ExamOfA, s.ActiveA));

            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetBatchPerformanceReportAsync(Admin, s.ExamOfA, s.BatchA)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetSectionAnalysisAsync(Admin, s.ActiveAAssignment, s.ActiveA)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetReviewLessonQuestionsAsync(Admin, s.ActiveAAssignment, s.ActiveA, s.Lesson1)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetStudentPlacementReportAsync(Admin, s.ActiveA, s.ActiveAAssignment, 0, 0)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetStudentPlacementReportAsync(Admin, s.ActiveA, 0, s.ExamOfA, 0)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetStudentPlacementReportPrintAsync(Admin, s.ExamOfA, s.ActiveA)).Outcome);
        }

        [Fact]
        public async Task Admin_Chrome_PartnerExam_RejectedBeforeLaunchingBrowser()
        {
            var (service, s, _, _, fetcher) = await NewAsync();

            Assert.Null(await service.BuildStudentReportChromePdfAsync(Admin, s.ExamOfA, s.ActiveA, "http://localhost/report"));

            fetcher.VerifyNoOtherCalls();
        }

        // =========================================
        // fallback الدفعة لا يسرّب نتائج نطاق آخر
        // =========================================
        [Fact]
        public async Task Admin_BatchStudents_InstituteExamOnPartnerBatch_DoesNotBorrowPartnerExamResults()
        {
            var (service, s, _, _, _) = await NewAsync();

            var data = await service.GetBatchStudentsAsync(Admin, s.InstituteExamOnPartnerBatch, s.BatchA);

            Assert.NotNull(data);
            var onlyPartner = data!.Students.Single(x => x.StudentId == s.OnlyPartnerExamA);
            Assert.Equal(0, onlyPartner.AssignmentId);
            Assert.Equal(0, onlyPartner.Score);

            // إجابته الفعلية على اختبار المعهد نفسه تبقى ظاهرة
            var active = data.Students.Single(x => x.StudentId == s.ActiveA);
            Assert.Equal(s.ActiveAInstituteAssignment, active.AssignmentId);
        }

        [Fact]
        public async Task Admin_Report_ByBatch_PicksInstituteAssignment_NotPartnerExam()
        {
            var (service, s, _, _, _) = await NewAsync();

            var result = await service.GetStudentPlacementReportAsync(Admin, s.OnlyPartnerExamA, 0, 0, s.BatchA);

            Assert.Equal(PlacementReportOutcome.NotFound, result.Outcome);
        }

        // =========================================
        // الشريك يرى اختباراته وطلابه فقط
        // =========================================
        [Fact]
        public async Task Partner_OwnExam_ListsOnlyOwnStudents_IncludingArchived()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            var data = await service.GetBatchStudentsAsync(scopeA, s.ExamOfA, s.BatchA);

            Assert.NotNull(data);
            Assert.Contains(data!.Students, x => x.StudentId == s.ActiveA);
            Assert.Contains(data.Students, x => x.StudentId == s.ArchivedA);
            Assert.DoesNotContain(data.Students, x => x.StudentId == s.ForeignInBatchA);
        }

        [Fact]
        public async Task Partner_BatchPerformance_ExcludesForeignStudentAttempts()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            var result = await service.GetBatchPerformanceReportAsync(scopeA, s.ExamOfA, s.BatchA);

            Assert.Equal(PlacementReportOutcome.Success, result.Outcome);
            var quant = result.Model!.SectionReports.Single(x => x.SectionTitle.Contains("الجبر"));
            // A1: Q1✓ Q2✓ ، مؤرشف: Q1✗ ، onlyPartner: Q1✓ — والطالب الأجنبي Q1✓ مستبعد
            Assert.Equal(3, quant.Correct);
            Assert.Equal(1, quant.Wrong);
        }

        [Fact]
        public async Task Partner_ReviewBatch_CountsOnlyOwnStudents()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            var model = await service.GetReviewLessonQuestionsBatchAsync(scopeA, s.ExamOfA, s.Lesson1);

            Assert.NotNull(model);
            var q1 = model!.Questions.Single(x => x.QuestionId == Q1);
            Assert.Equal(3, q1.TotalAnswers);
            Assert.Equal(0, q1.SkippedCount); // 3 طلاب للشريك، والرابع أجنبي
        }

        [Fact]
        public async Task Partner_OtherPartnerExam_IsMissingEverywhere()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            Assert.Null(await service.GetBatchStudentsAsync(scopeA, s.ExamOfB, s.BatchA));
            Assert.Null(await service.GetStudentExamDetailsAsync(scopeA, s.ExamOfB, s.ActiveA));
            Assert.Null(await service.GetReviewLessonQuestionsBatchAsync(scopeA, s.ExamOfB, s.Lesson1));
            Assert.Null(await service.BuildStudentReportPdfAsync(scopeA, s.ExamOfB, s.ActiveA));
            Assert.Null(await service.BuildStudentReportChromePdfAsync(scopeA, s.ExamOfB, s.ActiveA, "http://localhost/report"));

            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetBatchPerformanceReportAsync(scopeA, s.ExamOfB, s.BatchA)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetSectionAnalysisAsync(scopeA, s.StudentBAssignment, s.StudentB)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetReviewLessonQuestionsAsync(scopeA, s.StudentBAssignment, s.StudentB, s.Lesson1)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetStudentPlacementReportAsync(scopeA, s.ActiveA, 0, s.ExamOfB, 0)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetStudentPlacementReportPrintAsync(scopeA, s.ExamOfB, s.ActiveA)).Outcome);
        }

        [Fact]
        public async Task Partner_InstituteExam_IsMissing()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            Assert.Null(await service.GetBatchStudentsAsync(scopeA, s.InstituteExamOnPartnerBatch, s.BatchA));
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetStudentPlacementReportAsync(scopeA, s.ActiveA, 0, s.InstituteExamOnPartnerBatch, s.BatchA)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetSectionAnalysisAsync(scopeA, s.ActiveAInstituteAssignment, s.ActiveA)).Outcome);
        }

        [Fact]
        public async Task Partner_StudentOutsideBranches_ReturnsNull()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            Assert.Null(await service.GetStudentExamDetailsAsync(scopeA, s.ExamOfA, s.ForeignInBatchA));
            Assert.Null(await service.GetStudentExamDetailsAsync(scopeA, s.ExamOfA, s.FullStudent));
            Assert.Null(await service.BuildStudentReportPdfAsync(scopeA, s.ExamOfA, s.ForeignInBatchA));
            Assert.Null(await service.BuildStudentReportChromePdfAsync(scopeA, s.ExamOfA, s.ForeignInBatchA, "http://localhost/report"));

            var report = await service.GetStudentPlacementReportAsync(scopeA, s.ForeignInBatchA, 0, s.ExamOfA, s.BatchA);
            Assert.Equal(PlacementReportOutcome.NotFound, report.Outcome);
            Assert.Null(report.Model);

            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetStudentPlacementReportPrintAsync(scopeA, s.ExamOfA, s.ForeignInBatchA)).Outcome);
        }

        [Fact]
        public async Task Partner_BatchOutsideBranches_IsMissing()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            Assert.Null(await service.GetBatchStudentsAsync(scopeA, s.ExamOfA, s.BatchB));
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetBatchPerformanceReportAsync(scopeA, s.ExamOfA, s.BatchB)).Outcome);
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetStudentPlacementReportAsync(scopeA, s.ActiveA, 0, s.ExamOfA, s.BatchB)).Outcome);
        }

        [Fact]
        public async Task Partner_ReviewLesson_AssignmentOfOtherStudent_IsMissing()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            // تعيين الطالب النشط مع رقم طالب آخر من نفس الشريك
            var result = await service.GetReviewLessonQuestionsAsync(scopeA, s.ActiveAAssignment, s.ArchivedA, s.Lesson1);

            Assert.Equal(PlacementReportOutcome.NotFound, result.Outcome);
        }

        [Fact]
        public async Task Partner_ReviewBatch_LessonOutsideExam_DoesNotExposeLessonTitle()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            Assert.Null(await service.GetReviewLessonQuestionsBatchAsync(scopeA, s.ExamOfA, s.LessonNotInExams));
        }

        [Fact]
        public async Task Partner_ArchivedStudent_ResultsRemainVisible()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            var details = await service.GetStudentExamDetailsAsync(scopeA, s.ExamOfA, s.ArchivedA);
            Assert.NotNull(details);
            Assert.Equal(1, details!.WrongCount);

            var report = await service.GetStudentPlacementReportAsync(scopeA, s.ArchivedA, s.ArchivedAAssignment, 0, 0);
            Assert.Equal(PlacementReportOutcome.Success, report.Outcome);
            Assert.Equal(s.ArchivedAAssignment, report.Model!.AssignmentId);
        }

        [Fact]
        public async Task PartnerB_CannotReachPartnerAStudent()
        {
            var (service, s, _, scopeB, _) = await NewAsync();

            Assert.Null(await service.GetStudentExamDetailsAsync(scopeB, s.ExamOfB, s.ActiveA));
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetStudentPlacementReportAsync(scopeB, s.ActiveA, s.ActiveAAssignment, 0, 0)).Outcome);
        }

        [Fact]
        public async Task Partner_Pdf_OwnStudent_Generated()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            var file = await service.BuildStudentReportPdfAsync(scopeA, s.ExamOfA, s.ActiveA);

            Assert.NotNull(file);
            Assert.True(file!.Value.Pdf.Length > 0);
            Assert.Equal("تقرير_طالب activea.pdf", file.Value.FileName);
        }

        // =========================================
        // PPL-S6
        // =========================================
        [Fact]
        public async Task Partner_Print_ShowsStudentName_AdminModelUnchanged()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            var partner = await service.GetStudentPlacementReportPrintAsync(scopeA, s.ExamOfA, s.ActiveA);
            Assert.Equal("طالب activea", partner.Model!.StudentName);
            Assert.Equal(s.ActiveA, partner.Model.StudentId);

            var admin = await service.GetStudentPlacementReportPrintAsync(Admin, s.InstituteExam, s.FullStudent);
            Assert.Null(admin.Model!.StudentName);
            Assert.Equal(0, admin.Model.StudentId);
        }

        [Fact]
        public async Task Partner_Branding_NameOnly_WhenLogoIsOutsidePartnersFolder()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            var branding = await service.GetReportBrandingAsync(scopeA);

            Assert.Equal($"{Marker} A", branding!.PartnerName);
            Assert.Null(branding.LogoUrl);
            Assert.Null(await service.GetReportBrandingAsync(Admin));
        }

        [Fact]
        public async Task Partner_StudentExams_OnlyOwnExams()
        {
            var (service, s, scopeA, scopeB, _) = await NewAsync();

            var exams = await service.GetStudentPlacementExamsAsync(scopeA, s.ActiveA);
            Assert.Equal(new[] { s.ExamOfA }, exams.Select(x => x.ExamId));
            Assert.Equal(s.BatchA, exams[0].BatchId);
            Assert.Equal(s.ActiveAAssignment, exams[0].AssignmentId);

            Assert.Empty(await service.GetStudentPlacementExamsAsync(scopeB, s.ActiveA));
        }

        [Fact]
        public async Task Partner_BatchReports_ExamNotOnBatch_IsMissing()
        {
            var (service, s, scopeA, _, _) = await NewAsync();

            Assert.Null(await service.GetBatchStudentsAsync(scopeA, s.ExamOfA, s.InstituteBatch));
            Assert.Equal(PlacementReportOutcome.NotFound, (await service.GetBatchPerformanceReportAsync(scopeA, s.ExamOfA, s.InstituteBatch)).Outcome);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)] // ملف تالف أو صيغة غير مدعومة: التقرير يصدر بالاسم فقط
        public async Task Partner_Pdf_WithLogoFile_IsGenerated(bool validImage)
        {
            ApplicationDbContext db = TestDbContextFactory.CreateFreshContext(out _);
            var s = await SeedAsync(db);
            var root = Path.Combine(Path.GetTempPath(), "ppl-s6-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "uploads", "partners"));

            try
            {
                // PNG بكسل واحد
                var bytes = validImage
                    ? Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==")
                    : new byte[] { 1, 2, 3 };
                await File.WriteAllBytesAsync(Path.Combine(root, "uploads", "partners", "logo.png"), bytes);

                var partner = await db.Partners.FindAsync(s.PartnerA);
                partner!.LogoPath = "/uploads/partners/logo.png";
                await db.SaveChangesAsync();

                var environment = new Mock<IWebHostEnvironment>();
                environment.Setup(e => e.WebRootPath).Returns(root);

                var service = new PlacementExamReportService(
                    db,
                    new PlacementExamScopeGuard(),
                    new Mock<ITimeZoneService>().Object,
                    new ExamRecommendationService(),
                    new ReportBrowserProvider(new Mock<IBrowserFetcher>().Object),
                    environment.Object,
                    NullLogger<PlacementExamReportService>.Instance);
                var scope = PlacementScope.Partner(s.PartnerA, "a");

                var file = await service.BuildStudentReportPdfAsync(scope, s.ExamOfA, s.ActiveA);

                Assert.True(file!.Value.Pdf.Length > 0);
                Assert.Equal("/uploads/partners/logo.png", (await service.GetReportBrandingAsync(scope))!.LogoUrl);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Theory]
        [InlineData("<p>نص <b>السؤال</b></p>", "نص السؤال")]
        [InlineData("<script>alert(1)</script>", "alert(1)")]
        [InlineData("أ &amp; ب", "أ & ب")]
        [InlineData("   ", "—")]
        [InlineData(null, "—")]
        public void PlainText_RemovesTags(string? input, string expected)
            => Assert.Equal(expected, PlacementReportText.ToPlainText(input));
    }
}
