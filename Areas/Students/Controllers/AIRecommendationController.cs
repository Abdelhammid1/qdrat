using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.AI;
using System.Linq;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    public class AIRecommendationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AIRecommendationController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(int studentId)
        {
            var studentPerformances = _context.StudentPerformances.Where(sp => sp.StudentID == studentId).ToList();
            var curriculums = _context.Curriculums.Include(c => c.Sections).ToList();

            var recommendations = RecommendationEngine.GetRecommendedCurriculums(studentId, studentPerformances, curriculums);
            return View(recommendations);
        }
    }
}
