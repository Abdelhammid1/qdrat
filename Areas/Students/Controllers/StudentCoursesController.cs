using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Students;
using System.Text.Json;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    public class StudentCoursesController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentCoursesController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager)
        {
            _contextFactory = contextFactory;
            _userManager = userManager;
        }

        // ===============================
        // GET: اختيار الدورة
        // ===============================
        [HttpGet]
        public async Task<IActionResult> SelectCourse(string? returnUrl = null)
        {
            using var db = _contextFactory.CreateDbContext();

            ViewBag.ReturnUrl = returnUrl;

            var userId = _userManager.GetUserId(User);

            var studentId = await db.Students
                .Where(s => s.UserId == userId)
                .Select(s => s.StudentID)
                .FirstOrDefaultAsync();

            if (studentId == 0)
                return RedirectToAction("Login", "Account", new { area = "" });

            var courses = await (
                from e in db.StudentBatchEnrollments
                join b in db.Batches on e.BatchId equals b.Id
                join c in db.Courses on b.CourseId equals c.Id
                where e.StudentID == studentId
                select new StudentCourseSelectVm
                {
                    CourseId = c.Id,
                    CourseTitle = c.Name,
                    BatchId = b.Id,
                    BatchName = b.Name
                }
            ).AsNoTracking().ToListAsync();

            // ✅ لو عنده دورة واحدة → نختارها تلقائيًا
            if (courses.Count == 1 && string.IsNullOrEmpty(returnUrl))
            {
                var single = courses.First();

                var ctx = new StudentCourseContextVm
                {
                    CourseId = single.CourseId,
                    BatchId = single.BatchId,
                    CourseTitle = single.CourseTitle,
                    BatchTitle = single.BatchName
                };

                HttpContext.Session.SetString(
                    "StudentCourseContext",
                    JsonSerializer.Serialize(ctx)
                );

                return RedirectToAction(
                    "Index",
                    "StudentExamDashboard",
                    new { area = "Students" }
                );
            }

            return View(courses);
        }

        // ===============================
        // POST: تثبيت الدورة
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetCourse(
            int courseId,
            int batchId,
            string courseTitle,
            string batchTitle,
            string? returnUrl = null)
        {
            var ctx = new StudentCourseContextVm
            {
                CourseId = courseId,
                BatchId = batchId,
                CourseTitle = courseTitle,
                BatchTitle = batchTitle
            };

            HttpContext.Session.SetString(
                "StudentCourseContext",
                JsonSerializer.Serialize(ctx)
            );

            return Redirect(returnUrl ?? Url.Action(
                "Index",
                "StudentExamDashboard",
                new { area = "Students" })!);
        }
    }
}
