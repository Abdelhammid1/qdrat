using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using QdratNew.AI.Analysis;
using QdratNew.AI.MLModels.Branches;
using QdratNew.Data;
using QdratNew.Helpers;
using QdratNew.ViewModels.Branches;
using QdratNew.ViewModels.Students;
using Rotativa.AspNetCore;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class BranchSmartReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BranchSmartReportController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        public async Task<IActionResult> Analyze(int id)
        {
            var branch = await _context.Branches
                .Include(b => b.Students)
                    .ThenInclude(s => s.StudentPerformances)
                .Include(b => b.Courses)
                .Include(b => b.Projects)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (branch == null)
                return NotFound();

            var allPerformances = branch.Students
                .SelectMany(s => s.StudentPerformances)
                .ToList();

            // 🔁 تحويل البيانات إلى تنسيق ML
            var mlData = allPerformances
                .Select(p => new BranchPerformanceData
                {
                    EngagementRate = p.EngagementRate,
                    AttendanceCount = p.AttendanceCount,
                    StudyHours = p.StudyHours
                })
                .ToList();

            var analysis = BranchAIAnalyzer.Analyze(branch);
            var predictedScore = BranchMLAnalyzer.PredictScore(mlData);

            var model = new BranchSmartReportViewModel
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                City = branch.City,
                IsPartner = branch.IsPartner,
                EstablishedDate = branch.EstablishedDate,
                TotalStudents = branch.Students?.Count ?? 0,
                TotalCourses = branch.Courses?.Count ?? 0,
                TotalProjects = branch.Projects?.Count ?? 0,
                AveragePerformance = analysis.AveragePerformance,
                AverageEngagement = analysis.AverageEngagement,
                AverageAttendance = analysis.AverageAttendance,
                MLBasedScorePrediction = predictedScore,
                AiComment = analysis.AiComment,
                Recommendation = analysis.Recommendation,
                ScoreDistribution = allPerformances.Select(p => p.Score).ToList(),

                MonthlyPerformanceTrend = allPerformances
                    .GroupBy(p => p.ExamDate.ToString("yyyy-MM"))
                    .OrderBy(g => g.Key)
                    .Select(g => new PerformancePointViewModel
                    {
                        Month = g.Key,
                        AverageScore = g.Average(p => p.Score)
                    }).ToList(),

                TopStudents = branch.Students
                    .Where(s => s.StudentPerformances.Any())
                    .OrderByDescending(s => s.StudentPerformances.Average(p => p.Score))
                    .Take(5)
                    .Select(s => new StudentPerformanceMiniViewModel
                    {
                        StudentId = s.StudentID,
                        StudentName = s.FullName,
                        Score = s.StudentPerformances.Average(p => p.Score),
                        EngagementRate = s.StudentPerformances.Average(p => p.EngagementRate),
                        AttendanceCount = (int)Math.Round(s.StudentPerformances.Average(p => p.AttendanceCount)),
                        WeakTopics = s.StudentPerformances
                            .Where(p => !string.IsNullOrEmpty(p.WeakTopics))
                            .Select(p => p.WeakTopics)
                            .FirstOrDefault()
                    }).ToList()
            };

            return View("Analyze", model);
        }


        [HttpGet]
        public IActionResult GetQuickInsight(int id)
        {
            var branch = _context.Branches
                .Include(b => b.Students)
                    .ThenInclude(s => s.StudentPerformances)
                .Include(b => b.Courses)
                .Include(b => b.Projects)
                .FirstOrDefault(b => b.Id == id);

            if (branch == null)
                return NotFound();

            var ai = BranchAIAnalyzer.Analyze(branch);

            return Json(new
            {
                comment = ai.AiComment,
                recommendation = ai.Recommendation
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(int id)
        {
            var branch = await _context.Branches
                .Include(b => b.Students).ThenInclude(s => s.StudentPerformances)
                .Include(b => b.Courses)
                .Include(b => b.Projects)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (branch == null)
                return NotFound();

            var allPerformances = branch.Students
                .SelectMany(s => s.StudentPerformances)
                .ToList();

            // ✅ تحويل StudentPerformance إلى BranchPerformanceData
            var mlData = allPerformances
                .Select(p => new BranchPerformanceData
                {
                    EngagementRate = p.EngagementRate,
                    AttendanceCount = p.AttendanceCount,
                    StudyHours = p.StudyHours
                })
                .ToList();

            var analysis = BranchAIAnalyzer.Analyze(branch);
            var predictedScore = BranchMLAnalyzer.PredictScore(mlData);

            var model = new BranchSmartReportViewModel
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                City = branch.City,
                IsPartner = branch.IsPartner,
                EstablishedDate = branch.EstablishedDate,
                TotalStudents = branch.Students?.Count ?? 0,
                TotalCourses = branch.Courses?.Count ?? 0,
                TotalProjects = branch.Projects?.Count ?? 0,
                AveragePerformance = analysis.AveragePerformance,
                AverageEngagement = analysis.AverageEngagement,
                AverageAttendance = analysis.AverageAttendance,
                MLBasedScorePrediction = predictedScore,
                AiComment = analysis.AiComment,
                Recommendation = analysis.Recommendation,
                ScoreDistribution = allPerformances.Select(p => p.Score).ToList(),

                MonthlyPerformanceTrend = allPerformances
                    .GroupBy(p => p.ExamDate.ToString("yyyy-MM"))
                    .OrderBy(g => g.Key)
                    .Select(g => new PerformancePointViewModel
                    {
                        Month = g.Key,
                        AverageScore = g.Average(p => p.Score)
                    }).ToList(),

                TopStudents = branch.Students
                    .Where(s => s.StudentPerformances.Any())
                    .OrderByDescending(s => s.StudentPerformances.Average(p => p.Score))
                    .Take(5)
                    .Select(s => new StudentPerformanceMiniViewModel
                    {
                        StudentId = s.StudentID,
                        StudentName = s.FullName,
                        Score = s.StudentPerformances.Average(p => p.Score),
                        EngagementRate = s.StudentPerformances.Average(p => p.EngagementRate),
                        AttendanceCount = (int)Math.Round(s.StudentPerformances.Average(p => p.AttendanceCount)),
                        WeakTopics = s.StudentPerformances
                            .Where(p => !string.IsNullOrEmpty(p.WeakTopics))
                            .Select(p => p.WeakTopics)
                            .FirstOrDefault()
                    }).ToList()
            };

            return new ViewAsPdf("Analyze", model)
            {
                FileName = $"تقرير_الفرع_{model.BranchName}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait
            };
        }

    }
}
