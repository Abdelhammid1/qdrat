using Microsoft.AspNetCore.Mvc;
using QdratNew.Areas.Admin.Controllers;
using QdratNew.Data;
using QdratNew.ViewModels.Reports;
using Xunit;
using static QdratNew.Tests.PartnerPlacement.PlacementReportFixture;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S3.3 — مخرجات تقارير الأدمن مطابقة قبل/بعد نقل المنطق للخدمة.
    // الـ Snapshots سُجّلت من Controller الأدمن القديم على نفس البيانات الثابتة (EF InMemory).
    public class PlacementExamReportSnapshotTests
    {
        private static async Task<(PlacementExamsController Controller, PlacementReportSeed Seed)> NewAsync()
        {
            ApplicationDbContext db = TestDbContextFactory.CreateFreshContext(out _);
            var seed = await SeedAsync(db);
            return (PlacementReportHarness.NewAdminController(db), seed);
        }

        private static async Task VerifyAsync(string name, Func<PlacementExamsController, PlacementReportSeed, Task<IActionResult>> action)
        {
            var (controller, seed) = await NewAsync();
            var result = await action(controller, seed);
            PlacementReportHarness.VerifySnapshot(name, PlacementReportHarness.Describe(controller, result));
        }

        // ===== BatchPerformanceReport =====
        [Fact]
        public Task BatchPerformance_Institute()
            => VerifyAsync("BatchPerformance_Institute", (c, s) => c.BatchPerformanceReport(s.InstituteExam, s.InstituteBatch));

        [Fact]
        public Task BatchPerformance_MissingExam()
            => VerifyAsync("BatchPerformance_MissingExam", (c, s) => c.BatchPerformanceReport(999999, s.InstituteBatch));

        [Fact]
        public Task BatchPerformance_MissingBatch()
            => VerifyAsync("BatchPerformance_MissingBatch", (c, s) => c.BatchPerformanceReport(s.InstituteExam, 999999));

        // أرقام صريحة إلى جانب الـ Snapshot
        [Fact]
        public async Task BatchPerformance_Numbers()
        {
            var (controller, seed) = await NewAsync();

            var view = Assert.IsType<ViewResult>(await controller.BatchPerformanceReport(seed.InstituteExam, seed.InstituteBatch));
            var model = Assert.IsType<AdminExamSectionAnalysisViewModel>(view.Model);

            Assert.Equal("~/Areas/Admin/Views/PlacementExams/BatchPerformanceReport.cshtml", view.ViewName);
            Assert.Equal(2, model.SectionReports.Count);

            var quant = model.SectionReports.Single(x => x.SectionTitle.Contains("الجبر"));
            Assert.Equal(3, quant.TotalQuestions);
            // Q1: full✓ partial✗ noAssignment✓ ، Q2: full✗ ، Q3: full✗
            Assert.Equal(2, quant.Correct);
            Assert.Equal(3, quant.Wrong);
            Assert.Equal(40, quant.Score);

            var verbal = model.SectionReports.Single(x => x.SectionTitle.Contains("الاستيعاب"));
            Assert.Equal(1, verbal.Correct);
            Assert.Equal(0, verbal.Wrong);
            Assert.Equal(100, verbal.Score);
        }

        // ===== ReviewLessonQuestionsBatch =====
        [Fact]
        public Task ReviewBatch_Lesson1()
            => VerifyAsync("ReviewBatch_Lesson1", (c, s) => c.ReviewLessonQuestionsBatch(s.InstituteExam, s.Lesson1));

        [Fact]
        public Task ReviewBatch_PassageLesson()
            => VerifyAsync("ReviewBatch_PassageLesson", (c, s) => c.ReviewLessonQuestionsBatch(s.InstituteExam, s.Lesson3));

        [Fact]
        public Task ReviewBatch_LessonNotInExam()
            => VerifyAsync("ReviewBatch_LessonNotInExam", (c, s) => c.ReviewLessonQuestionsBatch(s.InstituteExam, s.LessonNotInExams));

        [Fact]
        public Task ReviewBatch_MissingExam()
            => VerifyAsync("ReviewBatch_MissingExam", (c, s) => c.ReviewLessonQuestionsBatch(999999, s.Lesson1));

        [Fact]
        public Task ReviewBatch_MissingLesson()
            => VerifyAsync("ReviewBatch_MissingLesson", (c, s) => c.ReviewLessonQuestionsBatch(s.InstituteExam, 999999));

        // ===== ExamSectionAnalysis =====
        [Fact]
        public Task SectionAnalysis_Full()
            => VerifyAsync("SectionAnalysis_Full", (c, s) => c.ExamSectionAnalysis(s.FullAssignment, s.FullStudent));

        [Fact]
        public Task SectionAnalysis_WrongStudent()
            => VerifyAsync("SectionAnalysis_WrongStudent", (c, s) => c.ExamSectionAnalysis(s.FullAssignment, s.PartialStudent));

        // ===== ReviewLessonQuestions =====
        [Fact]
        public Task ReviewLesson_Full()
            => VerifyAsync("ReviewLesson_Full", (c, s) => c.ReviewLessonQuestions(s.FullAssignment, s.FullStudent, s.Lesson1));

        [Fact]
        public Task ReviewLesson_NoQuestions()
            => VerifyAsync("ReviewLesson_NoQuestions", (c, s) => c.ReviewLessonQuestions(s.FullAssignment, s.FullStudent, s.LessonNotInExams));

        [Fact]
        public Task ReviewLesson_MissingAssignment()
            => VerifyAsync("ReviewLesson_MissingAssignment", (c, s) => c.ReviewLessonQuestions(999999, s.FullStudent, s.Lesson1));

        [Fact]
        public Task ReviewLesson_MissingLesson()
            => VerifyAsync("ReviewLesson_MissingLesson", (c, s) => c.ReviewLessonQuestions(s.FullAssignment, s.FullStudent, 999999));

        // ===== StudentExamDetails =====
        [Fact]
        public Task StudentDetails_Full()
            => VerifyAsync("StudentDetails_Full", (c, s) => c.StudentExamDetails(s.InstituteExam, s.FullStudent));

        [Fact]
        public Task StudentDetails_MissingStudent()
            => VerifyAsync("StudentDetails_MissingStudent", (c, s) => c.StudentExamDetails(s.InstituteExam, 999999));

        // ===== AdminPlacementReport بكل مسارات تحديد التعيين =====
        [Fact]
        public Task Report_ByAssignment()
            => VerifyAsync("Report_ByAssignment", (c, s) => c.AdminPlacementReport(s.FullStudent, s.FullAssignment));

        [Fact]
        public Task Report_ByExamAndBatch()
            => VerifyAsync("Report_ByExamAndBatch", (c, s) => c.AdminPlacementReport(s.FullStudent, 0, s.InstituteExam, s.InstituteBatch));

        [Fact]
        public Task Report_ByExamOnly()
            => VerifyAsync("Report_ByExamOnly", (c, s) => c.AdminPlacementReport(s.PartialStudent, 0, s.InstituteExam));

        [Fact]
        public Task Report_NoAssignmentRow()
            => VerifyAsync("Report_NoAssignmentRow", (c, s) => c.AdminPlacementReport(s.NoAssignmentStudent, 0, s.InstituteExam));

        [Fact]
        public Task Report_MissingStudent()
            => VerifyAsync("Report_MissingStudent", (c, s) => c.AdminPlacementReport(999999, s.FullAssignment));

        [Fact]
        public Task Report_MissingExam()
            => VerifyAsync("Report_MissingExam", (c, s) => c.AdminPlacementReport(s.FullStudent, 0));

        // ===== AdminPlacementReportPrint =====
        [Fact]
        public Task Print_Full()
            => VerifyAsync("Print_Full", (c, s) => c.AdminPlacementReportPrint(s.InstituteExam, s.FullStudent));

        [Fact]
        public Task Print_NoAttempts()
            => VerifyAsync("Print_NoAttempts", (c, s) => c.AdminPlacementReportPrint(s.InstituteExam, s.OtherExamStudent));

        [Fact]
        public Task Print_MissingStudent()
            => VerifyAsync("Print_MissingStudent", (c, s) => c.AdminPlacementReportPrint(s.InstituteExam, 999999));

        // ===== BatchStudents =====
        [Fact]
        public Task BatchStudents_Institute()
            => VerifyAsync("BatchStudents_Institute", (c, s) => c.BatchStudents(s.InstituteExam, s.InstituteBatch));

        [Fact]
        public Task BatchStudents_MissingExam()
            => VerifyAsync("BatchStudents_MissingExam", (c, s) => c.BatchStudents(999999, s.InstituteBatch));

        // ===== DownloadStudentReport =====
        [Fact]
        public Task Pdf_Full()
            => VerifyAsync("Pdf_Full", (c, s) => c.DownloadStudentReport(s.InstituteExam, s.FullStudent));

        [Fact]
        public Task Pdf_MissingStudent()
            => VerifyAsync("Pdf_MissingStudent", (c, s) => c.DownloadStudentReport(s.InstituteExam, 999999));
    }
}
