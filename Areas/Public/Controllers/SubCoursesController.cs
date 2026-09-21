using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities.Frontend;

namespace QdratNew.Areas.Public.Controllers
{
    [Area("Public")]
    public class SubCoursesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubCoursesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ عرض تفاصيل الدورة الفرعية (Landing Page)
        [HttpGet("/Public/SubCourses/Details/{slug}")]
        public async Task<IActionResult> Details(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return NotFound();

            var subCourse = await _context.SubCourses
                .Include(s => s.FrontendCourse)
                .FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);

            if (subCourse == null)
                return NotFound("❌ لم يتم العثور على الدورة المطلوبة.");

            // ✅ تمرير بيانات الدورة إلى الـ View
            return View("SubCourseDetails", subCourse);
        }
    }
}
