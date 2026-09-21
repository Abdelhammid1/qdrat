using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Curriculum;
using System.Linq;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer")]
    public class AIAnalysisController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AIAnalysisController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var data = _context.Curriculums
     .Select(c => new CurriculumPerformanceViewModel
     {
         CurriculumTitle = c.Title,
         TotalStudents = _context.StudentPerformances
             .Count(sp => sp.Section.CurriculumId == c.Id),

         AverageScore = _context.StudentPerformances
             .Where(sp => sp.Section.CurriculumId == c.Id)
             .Select(sp => (double?)sp.Score)
             .ToList()
             .DefaultIfEmpty(0)
             .Average() ?? 0
     }).ToList();


            return View(data);
        }


    }
}
