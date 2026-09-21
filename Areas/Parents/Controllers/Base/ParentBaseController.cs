using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Parents.Interfaces;
using System.Threading.Tasks;

namespace QdratNew.Areas.Parents.Controllers.Base
{
    [Area("Parents")]
    [Authorize(Roles = "Parent,SuperAdmin,Owner,Developer")]
    public abstract class ParentBaseController : Controller
    {
        protected readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        protected readonly UserManager<ApplicationUser> _userManager;
        protected readonly IParentAccessService _parentAccessService;

        protected int ParentId { get; private set; }
        protected int? SelectedStudentId { get; private set; }

        protected ParentBaseController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IParentAccessService parentAccessService)
        {
            _contextFactory = contextFactory;
            _userManager = userManager;
            _parentAccessService = parentAccessService;
        }

        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var userId = _userManager.GetUserId(User);

            // حماية Session
            var sessionUser = HttpContext.Session.GetString("ParentSessionKey");
            if (string.IsNullOrEmpty(sessionUser) || sessionUser != userId)
            {
                HttpContext.Session.Clear();
                if (!string.IsNullOrEmpty(userId))
                    HttpContext.Session.SetString("ParentSessionKey", userId);
            }

            // تحديد ولي الأمر
            var parentId = await _parentAccessService.GetCurrentParentIdAsync(userId ?? string.Empty);
            if (parentId == null)
            {
                context.Result = RedirectToAction("Login", "Account", new { area = "" });
                return;
            }

            ParentId = parentId.Value;
            HttpContext.Items["ParentId"] = ParentId;

            // تحديد الطالب المختار من QueryString أو Session
            int? studentId = null;
            if (HttpContext.Request.Query.TryGetValue("studentId", out var sqVal) &&
                int.TryParse(sqVal, out int sqId))
            {
                studentId = sqId;
                HttpContext.Session.SetString("ParentSelectedStudent", sqId.ToString());
            }
            else
            {
                var cached = HttpContext.Session.GetString("ParentSelectedStudent");
                if (!string.IsNullOrEmpty(cached) && int.TryParse(cached, out int cId))
                    studentId = cId;
            }

            // التحقق من صلاحية الوصول للطالب
            if (studentId.HasValue)
            {
                bool canAccess = await _parentAccessService.CanAccessStudentAsync(
                    userId ?? string.Empty, studentId.Value);

                if (!canAccess) studentId = null;
            }

            // إذا لم يُحدد طالب، اختر الأول
            if (!studentId.HasValue)
            {
                using var db = _contextFactory.CreateDbContext();
                var firstStudent = await db.Students
                    .AsNoTracking()
                    .Where(s => s.ParentId == parentId)
                    .Select(s => (int?)s.StudentID)
                    .FirstOrDefaultAsync();

                if (firstStudent.HasValue)
                {
                    studentId = firstStudent;
                    HttpContext.Session.SetString("ParentSelectedStudent", firstStudent.Value.ToString());
                }
            }

            SelectedStudentId = studentId;
            HttpContext.Items["SelectedStudentId"] = studentId;
            ViewData["SelectedStudentId"] = studentId;
            ViewData["ParentId"] = ParentId;

            await next();
        }
    }
}
