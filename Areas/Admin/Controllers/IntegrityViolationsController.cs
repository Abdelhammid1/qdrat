using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.IntegrityViolations;
using System.Security.Claims;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = AdminPermissionPolicies.IntegrityViolations_Read)]
    public class IntegrityViolationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IIntegrityGuardService _integrityGuard;
        private readonly IAdminActivityLogger _activityLogger;

        public IntegrityViolationsController(
            ApplicationDbContext context,
            IIntegrityGuardService integrityGuard,
            IAdminActivityLogger activityLogger)
        {
            _context = context;
            _integrityGuard = integrityGuard;
            _activityLogger = activityLogger;
        }

        private string CurrentUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private string CurrentUserName() =>
            User.Identity?.Name ?? "غير معروف";

        [HttpGet]
        public async Task<IActionResult> Index(bool showResolved = false, IntegrityAttemptType? attemptType = null, int? attemptEntityId = null)
        {
            var query = _context.IntegrityViolationLogs.AsNoTracking();

            if (!showResolved)
                query = query.Where(v => !v.IsResolved);

            if (attemptType.HasValue)
                query = query.Where(v => v.AttemptType == attemptType.Value);

            if (attemptEntityId.HasValue)
                query = query.Where(v => v.AttemptEntityId == attemptEntityId.Value);

            var rows = await (
                from v in query
                join s in _context.Students.AsNoTracking() on v.StudentId equals s.StudentID into sj
                from s in sj.DefaultIfEmpty()
                orderby v.DetectedAt descending
                select new IntegrityViolationRowVM
                {
                    Id = v.Id,
                    StudentId = v.StudentId,
                    StudentName = s != null ? s.FullName : null,
                    AttemptType = v.AttemptType,
                    AttemptEntityId = v.AttemptEntityId,
                    ViolationType = v.ViolationType,
                    DetectedAt = v.DetectedAt,
                    IpAddress = v.IpAddress,
                    PageUrl = v.PageUrl,
                    IsResolved = v.IsResolved,
                    ResolvedAt = v.ResolvedAt,
                    ResolutionNote = v.ResolutionNote,
                    SelfResolved = v.SelfResolved
                }
            ).ToListAsync();

            ViewBag.ShowResolved = showResolved;
            ViewBag.FilterAttemptType = attemptType;
            ViewBag.FilterAttemptEntityId = attemptEntityId;

            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPermissionPolicies.IntegrityViolations_Resolve)]
        public async Task<IActionResult> Resolve(int id, string? note)
        {
            var resolved = await _integrityGuard.ResolveAsync(id, CurrentUserId(), note);

            if (resolved)
            {
                await _activityLogger.LogAsync(
                    "IntegrityViolation_Resolve",
                    $"إعادة فتح محاولة موقوفة (سجل رقم {id})" + (string.IsNullOrWhiteSpace(note) ? "" : $" — ملاحظة: {note}"),
                    CurrentUserId(),
                    CurrentUserName());

                TempData["Message"] = "✅ تم إعادة فتح المحاولة بنجاح";
            }
            else
            {
                TempData["Error"] = "تعذّر العثور على سجل المخالفة";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// إعادة فتح مباشرة لطالب معيّن من داخل صفحات إدارة الواجب/الاختبار (بدون معرفة رقم سجل المخالفة)،
        /// يُستخدم من: HomeworkManagement/StudentsReport، PlacementExams/BatchStudents، PerformanceIndicatorExams/ExamStudents.
        /// يعود لنفس الصفحة التي جاء منها الطلب (returnUrl محلي فقط).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPermissionPolicies.IntegrityViolations_Resolve)]
        public async Task<IActionResult> ResolveByAttempt(int studentId, IntegrityAttemptType attemptType, int attemptEntityId, string? note, string? returnUrl)
        {
            var resolved = await _integrityGuard.ResolveActiveAsync(studentId, attemptType, attemptEntityId, CurrentUserId(), note);

            if (resolved)
            {
                await _activityLogger.LogAsync(
                    "IntegrityViolation_Resolve",
                    $"إعادة فتح محاولة موقوفة للطالب رقم {studentId} ({attemptType}, EntityId={attemptEntityId})" + (string.IsNullOrWhiteSpace(note) ? "" : $" — ملاحظة: {note}"),
                    CurrentUserId(),
                    CurrentUserName());

                TempData["Message"] = "✅ تم إعادة فتح المحاولة بنجاح";
            }
            else
            {
                TempData["Error"] = "تعذّر العثور على مخالفة نشطة لهذا الطالب";
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }
    }
}
