// QdratNew.Areas.Admin.Controllers.BranchesAIAnalysisController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.AI.Recommendations;
using QdratNew.Data;
using QdratNew.ViewModels.Analytics;
using QdratNew.ViewModels.Branches;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class BranchesAIAnalysisController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BranchesAIAnalysisController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var branch = await _context.Branches
                .Include(b => b.Students)
                .Include(b => b.Courses)
                .Include(b => b.Projects)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (branch == null) return NotFound();

            var performances = branch.Students
                .SelectMany(s => s.StudentPerformances)
                .GroupBy(p => p.ExamDate.ToString("yyyy-MM"))
                    .Select(g => new QdratNew.ViewModels.Branches.PerformancePointViewModel
                    {
                        Month = g.Key,
                        AverageScore = g.Average(p => p.Score)
                    }).ToList();


            var model = new BranchAIAnalysisViewModel
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                City = branch.City,
                IsPartner = branch.IsPartner,
                TotalStudents = branch.Students.Count,
                TotalCourses = branch.Courses.Count,
                TotalProjects = branch.Projects.Count,
                AveragePerformance = performances.Any() ? performances.Average(p => p.AverageScore) : 0,
                MonthlyPerformanceTrend = performances,
            };

            model.AIRecommendations = BranchSmartAnalyzer.Generate(model);

            return View(model);
        }
    }
}
