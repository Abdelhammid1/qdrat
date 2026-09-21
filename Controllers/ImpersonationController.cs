using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services;
using System.Security.Claims;

namespace QdratNew.Controllers
{
    [Authorize]
    public class ImpersonationController : Controller
    {
        private readonly ImpersonationService _impersonation;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ImpersonationController(
            ImpersonationService impersonation,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _impersonation = impersonation;
            _context       = context;
            _userManager   = userManager;
        }

        // POST /Impersonation/Cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel()
        {
            var data = _impersonation.Read();
            var returnUserId = data?.TargetUserId ?? "";

            _impersonation.Clear();
            HttpContext.Session.Remove("UserSessionKey");
            HttpContext.Session.Remove("StudentCourseContext");

            TempData["SuccessMessage"] = "✅ تم إنهاء جلسة التخفي. مرحباً بك مجدداً.";

            if (!string.IsNullOrEmpty(returnUserId))
                return RedirectToAction("Details", "Users", new { area = "Admin", id = returnUserId });

            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }

        // POST /Impersonation/StartAs — متاح لـ Owner/Developer/SuperAdmin + حاملي UserImpersonationAccess
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartAs(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isSystemRole  = User.IsInRole("SuperAdmin") || User.IsInRole("Owner") || User.IsInRole("Developer");

            if (!isSystemRole)
            {
                var hasAccess = await _context.UserImpersonationAccesses
                    .AsNoTracking()
                    .AnyAsync(x => x.UserId == currentUserId && x.IsActive);

                if (!hasAccess)
                    return Forbid();
            }

            var target = await _userManager.FindByIdAsync(userId);
            if (target == null)
                return NotFound();

            _impersonation.Clear();

            var targetRoles = await _userManager.GetRolesAsync(target);

            _impersonation.Start(new ImpersonationData
            {
                AdminId      = currentUserId,
                TargetUserId = target.Id,
                TargetName   = target.FullName ?? target.UserName ?? "",
                TargetEmail  = target.Email ?? "",
                TargetRoles  = targetRoles.ToList(),
                StartUtc     = DateTime.UtcNow,
            });

            if (targetRoles.Contains("Student"))
                return RedirectToAction("Index", "StudentProfile", new { area = "Students" });
            if (targetRoles.Contains("Parent"))
                return RedirectToAction("Index", "Dashboard", new { area = "Parents" });
            if (targetRoles.Contains("Employee"))
                return RedirectToAction("Index", "EmployeeDashboard", new { area = "Admin" });
            if (targetRoles.Any(r => r == "Instructor" || r == "Teacher" || r == "PartnerInstructor"))
                return RedirectToAction("Index", "InstructorDashboard", new { area = "Instructors" });
            if (targetRoles.Any(r => r is "SuperAdmin" or "Owner" or "Developer" or "Admin" or "DataEntry"))
                return RedirectToAction("Index", "AdminOperationsDashboard", new { area = "Admin" });

            return RedirectToAction("Index", "StudentProfile", new { area = "Students" });
        }
    }
}
