using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities.Frontend;

namespace QdratNew.Areas.Public.Controllers
{
    [Area("Public")]
    public class FrontendCoursesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FrontendCoursesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🟢 عرض تفاصيل الدورة
        [HttpGet("Public/FrontendCourses/Details/{slug}")]
        public async Task<IActionResult> Details(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return NotFound("❌ لم يتم تمرير مسار صالح.");

            var course = await _context.FrontendCourses
                .Include(c => c.SubCourses.Where(s => s.IsActive))
                .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive);

            if (course == null)
                return NotFound("❌ لم يتم العثور على الدورة المطلوبة.");

            // نمرر الكورس إلى الـ View
            return View("ProgramDetails", course);
        }

        [HttpGet("Public/FrontendCourses/HomeList")]
        public async Task<IActionResult> HomeList()
        {
            var courses = await _context.FrontendCourses
                .Include(c => c.SubCourses.Where(s => s.IsActive))
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .ToListAsync();

            return PartialView("_ProgramsAndCoursesSection", courses);
        }





        // 🟡 عرض قسم فرعي من الدورة (صفحة المزيد)
        public IActionResult Section(int id)
        {
            var section = _context.FrontendCourseSections
                .Include(s => s.FrontendCourse)
                .FirstOrDefault(s => s.Id == id && s.IsActive);

            if (section == null)
                return NotFound("❌ القسم غير موجود.");

            return View(section);
        }

        // 📝 عرض نموذج التسجيل
   
        // ✅ صفحة الشكر بعد التسجيل
        public IActionResult Thanks(int id)
        {
            var course = _context.FrontendCourses.Find(id);
            ViewBag.CourseTitle = course?.Title;
            return View();
        }
    }
}
