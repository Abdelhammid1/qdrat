using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Instructor;
using QdratNew.ViewModels.Section;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class InstructorSmartEvaluationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InstructorSmartEvaluationController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int instructorId)
        {
            var instructor = await _context.Instructors.FindAsync(instructorId);
            if (instructor == null)
                return NotFound();

            var performances = await _context.StudentPerformances
                .Include(sp => sp.Section)
                .Include(sp => sp.Curriculum)
                .Where(sp =>
                    sp.Curriculum.CurriculumInstructors.Any(ci => ci.InstructorId == instructorId)
                    && sp.SectionId != null
                )
                .ToListAsync();

            var sectionEval = performances
                .GroupBy(sp => sp.Section.Title)
                .Select(g => new SectionEvaluationAIViewModel
                {
                    SectionTitle = g.Key,
                    AverageScore = Math.Round(g.Average(sp => sp.Score), 2),
                    SuccessRate = Math.Round(g.Count(sp => sp.Score >= 60) * 100.0 / g.Count(), 1),
                    StudentCount = g.Count(),
                    DifficultyLevel = g.Average(sp => sp.Score) < 50 ? "صعب" :
                                      g.Average(sp => sp.Score) < 70 ? "متوسط" : "سهل",
                    AIComment = "تحليل الذكاء الاصطناعي سيُضاف"
                }).ToList();

            var overallScore = sectionEval.Any() ? sectionEval.Average(s => s.AverageScore) : 0;

            var vm = new InstructorSmartEvaluationViewModel
            {
                InstructorId = instructorId,
                InstructorName = instructor.FullName,
                AIOverallScore = overallScore,
                AIEvaluationComment = overallScore > 85 ? "أداء ممتاز" :
                                      overallScore > 70 ? "أداء جيد" :
                                      overallScore > 50 ? "يحتاج تحسين" : "ضعيف",
                SectionsEvaluation = sectionEval,
                AIRecommendations = new List<string>
            {
                "راجع المحاور ذات الأداء الضعيف.",
                "استخدم وسائل شرح تفاعلية.",
                "تابع تقدم الطلاب باستمرار."
            },
                SectionTitles = sectionEval.Select(s => s.SectionTitle).ToList(),
                AverageScores = sectionEval.Select(s => s.AverageScore).ToList()
            };

            return View(vm);
        }
    }

}
