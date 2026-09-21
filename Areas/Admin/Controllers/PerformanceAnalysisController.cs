using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using QdratNew.Data;
using QdratNew.Services.Interfaces;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class PerformanceAnalysisController : Controller
    {
        private readonly IPerformanceReportService _reportService;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        public PerformanceAnalysisController(IPerformanceReportService reportService, IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _reportService = reportService;
            _contextFactory = contextFactory;
        }

        [HttpGet]
        public async Task<IActionResult> ExportToPdf(int batchId)
        {
            var report = await _reportService.GetBatchPerformanceReportAsync(batchId);
            return new Rotativa.AspNetCore.ViewAsPdf("BatchReport", report)
            {
                FileName = $"تقرير_مؤشر_الأداء_دفعة_{batchId}.pdf",
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape,
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }

        [HttpGet]
        public async Task<IActionResult> BatchReport(int batchId, int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ✅ التحقق من القيم
            if (examId == 0)
                return RedirectToAction("Index", "PerformanceDashboard");

            var batch = await _context.Batches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId);

            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Curriculum)
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (batch == null || exam == null)
                return NotFound("⚠️ لم يتم العثور على بيانات الدفعة أو الاختبار.");

            // ✅ جلب نتائج الاختبار لكل طالب في الدفعة
            var results = await _context.StudentIndicatorResults
                .Include(r => r.Student)
                .Include(r => r.Section)
                .Where(r => r.PerformanceIndicatorExamId == examId)
                .ToListAsync();

            if (!results.Any())
                return View("BatchReport", new QdratNew.ViewModels.Reports.BatchPerformanceReportViewModel
                {
                    BatchId = batchId,
                    BatchName = batch.Name,
                    ExamId = examId,
                    ExamTitle = exam.Title,
                    CurriculumTitle = exam.Curriculum?.Title ?? "—",
                    TotalStudents = 0,
                    TestedStudents = 0,
                    NotTestedStudents = 0,
                    PassedStudents = 0,
                    FailedStudents = 0,
                    AveragePercent = 0,
                    SectionStats = new List<QdratNew.ViewModels.Reports.SectionSummaryVm>(),
                    TopSections = new List<QdratNew.ViewModels.Reports.SectionSummaryVm>(),
                    WeakSections = new List<QdratNew.ViewModels.Reports.SectionSummaryVm>(),
                    RemedialStudents = new List<QdratNew.ViewModels.Reports.RemedialStudentVm>()
                });

            // ✅ تحليل عام
            int totalStudents = results.Select(r => r.StudentId).Distinct().Count();
            int passed = results.GroupBy(r => r.StudentId)
                .Count(g => g.Average(x => x.ScorePercent) >= 60);
            int failed = totalStudents - passed;
            double batchAverage = Math.Round(results.Average(r => r.ScorePercent), 1);

            // ✅ تحليل المحاور
            var sectionStats = results
                .GroupBy(r => r.Section.Title)
                .Select(g => new QdratNew.ViewModels.Reports.SectionSummaryVm
                {
                    SectionTitle = g.Key,
                    AvgScore = Math.Round(g.Average(x => x.ScorePercent), 1),
                    Passed = g.Count(x => x.ScorePercent >= 60),
                    Failed = g.Count(x => x.ScorePercent < 60)
                })
                .OrderByDescending(x => x.AvgScore)
                .ToList();

            // ✅ أقوى وأضعف المحاور
            var topSections = sectionStats.Take(3).ToList();
            var weakSections = sectionStats.OrderBy(x => x.AvgScore).Take(3).ToList();

            // ✅ الطلاب المحتاجين جلسة علاجية
            var remedialStudents = results
                .GroupBy(r => r.Student)
                .Where(g => g.Any(x => x.ScorePercent < 60))
                .Select(g => new QdratNew.ViewModels.Reports.RemedialStudentVm
                {
                    StudentId = g.Key.StudentID,
                    StudentName = g.Key.FullName,
                    AveragePercent = Math.Round(g.Average(x => x.ScorePercent), 1)
                })
                .OrderBy(x => x.AveragePercent)
                .ToList();

            // ✅ بناء النموذج النهائي
            var vm = new QdratNew.ViewModels.Reports.BatchPerformanceReportViewModel
            {
                BatchId = batchId,
                BatchName = batch.Name,
                ExamId = examId,
                ExamTitle = exam.Title,
                CurriculumTitle = exam.Curriculum?.Title ?? "—",
                TotalStudents = totalStudents,
                TestedStudents = totalStudents,
                NotTestedStudents = 0, // ← يمكن حسابها لاحقاً إن وجد ربط enrollment
                PassedStudents = passed,
                FailedStudents = failed,
                AveragePercent = batchAverage,
                SectionStats = sectionStats,
                TopSections = topSections,
                WeakSections = weakSections,
                RemedialStudents = remedialStudents
            };

            return View("BatchPerformanceReport", vm);
        }

    }
}
