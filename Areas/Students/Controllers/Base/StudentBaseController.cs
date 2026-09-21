using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using System.Text.Json;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public abstract class StudentBaseController : Controller
    {
        protected readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        protected readonly UserManager<ApplicationUser> _userManager;

        protected int StudentId { get; private set; }
        protected int ActiveCourseId { get; private set; }
        protected int ActiveBatchId { get; private set; }

        protected StudentCourseContextVm? CourseContext { get; private set; }

        protected StudentBaseController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager)
        {
            _contextFactory = contextFactory;
            _userManager = userManager;
        }

        public override async Task OnActionExecutionAsync(
     ActionExecutingContext context,
     ActionExecutionDelegate next)
        {
            using var db = _contextFactory.CreateDbContext();

            // =====================================
            // 🔐 حماية من تسريب Session بين المستخدمين
            // =====================================
            var userId = _userManager.GetUserId(User);

            var sessionUser = HttpContext.Session.GetString("UserSessionKey");

            if (string.IsNullOrEmpty(sessionUser) || sessionUser != userId)
            {
                HttpContext.Session.Clear();

                // إعادة ربط الجلسة بالمستخدم الحالي
                HttpContext.Session.SetString("UserSessionKey", userId);
            }



            // =========================
            // 1) تحديد الطالب
            // =========================
            var student = await db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student == null)
            {
                context.Result = RedirectToAction("Login", "Account", new { area = "" });
                return;
            }

            StudentId = student.StudentID;

            HttpContext.Items["StudentId"] = StudentId;

            // =========================
            // 2) منع Loop عند اختيار الدورة
            // =========================
            var controller = context.RouteData.Values["controller"]?.ToString();

            if (controller == "StudentCourses"
               || controller == "CourseSwitch"
               || controller == "StudentIndividualExams"
               || controller == "StudentExamResults"
               || controller == "StudentExamFlow"
               || controller == "StudentProfile") // لا يحتاج course context
            {
                await next();
                return;
            }



            // =========================
            // 3) قراءة Course Context من Session
            // =========================
            var json = HttpContext.Session.GetString("StudentCourseContext");
            if (!string.IsNullOrWhiteSpace(json))
            {
                var ctx = JsonSerializer.Deserialize<StudentCourseContextVm>(json);
                if (ctx != null && ctx.CourseId > 0 && ctx.BatchId > 0)
                {
                    ApplyContext(ctx);
                    await next();
                    return;
                }
            }

            // =========================
            // 4) جلب تسجيلات الطالب
            // =========================
            var enrollments = await (
                from e in db.StudentBatchEnrollments
                join b in db.Batches on e.BatchId equals b.Id
                join c in db.Courses on b.CourseId equals c.Id
                where e.StudentID == StudentId
                select new StudentCourseContextVm
                {
                    CourseId = c.Id,
                    CourseTitle = c.Name,
                    BatchId = b.Id,
                    BatchTitle = b.Name
                }
            ).AsNoTracking().ToListAsync();

            if (!enrollments.Any())
            {
                context.Result = RedirectToAction(
                    "ErrorMessage",
                    "Home",
                    new { area = "", msg = "لا توجد دورات مسجلة لك." });
                return;
            }

            // =========================
            // 5) دورة واحدة → اختيار تلقائي
            // =========================
            if (enrollments.Count == 1)
            {
                var single = enrollments.First();
                HttpContext.Session.SetString(
                    "StudentCourseContext",
                    JsonSerializer.Serialize(single));

                ApplyContext(single);
                await next();
                return;
            }

            // =========================
            // 6) أكثر من دورة → شاشة اختيار
            // =========================
            context.Result = RedirectToAction(
                "SelectCourse",
                "StudentCourses",
                new
                {
                    area = "Students",
                    returnUrl = HttpContext.Request.Path + HttpContext.Request.QueryString
                });
        }

        private void ApplyContext(StudentCourseContextVm ctx)
        {
            ActiveCourseId = ctx.CourseId;
            ActiveBatchId = ctx.BatchId;
            CourseContext = ctx;

            HttpContext.Items["ActiveCourseId"] = ctx.CourseId;
            HttpContext.Items["ActiveBatchId"] = ctx.BatchId;

            ViewData["ActiveCourseTitle"] = ctx.CourseTitle;
            ViewData["ActiveBatchTitle"] = ctx.BatchTitle;
        }

        private void SetContext(StudentCourseContextVm ctx)
        {
            CourseContext = ctx;
            ActiveCourseId = ctx.CourseId;
            ActiveBatchId = ctx.BatchId;

            // متاح لكل View
            ViewData["ActiveCourseTitle"] = ctx.CourseTitle;
            ViewData["ActiveBatchTitle"] = ctx.BatchTitle;
        }
    }

    // ====================================================
    // Context ViewModel
    // ====================================================
    public class StudentCourseContextVm
    {
        public int CourseId { get; set; }
        public int BatchId { get; set; }
        public string CourseTitle { get; set; } = "";
        public string BatchTitle { get; set; } = "";
    }
}
