using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Entities;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services.Admin;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Areas.Admin.Controllers
{
    /// <summary>
    /// RTK-S6.4 (D9): تقارير ولي الأمر والدفعة — للأدمن فقط. المنطق في IRemedialTrackReportService؛ الكنترولر رفيع.
    /// نطاق الدفعات يُحسب هنا (IEmployeeBatchAccessService) ويُفرض داخل الخدمة. لا يُعرض رقم مرجعي ولا بيانات طلاب آخرين.
    /// </summary>
    [Area("Admin")]
    public class RemedialTrackReportsController : Controller
    {
        private readonly IRemedialTrackReportService _reports;
        private readonly IEmployeeBatchAccessService _batchAccess;
        private readonly UserManager<ApplicationUser> _userManager;

        public RemedialTrackReportsController(
            IRemedialTrackReportService reports,
            IEmployeeBatchAccessService batchAccess,
            UserManager<ApplicationUser> userManager)
        {
            _reports = reports;
            _batchAccess = batchAccess;
            _userManager = userManager;
        }

        [HttpGet]
        [AdminPermission("RemedialTrackReports", "Read")]
        public async Task<IActionResult> Student(int enrollmentId, CancellationToken ct)
        {
            var vm = await _reports.GetParentReportAsync(enrollmentId, await ScopeAsync(), ct);
            if (vm is null) return NotFound();

            vm.CanEditNote = await HasEditAsync();
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTrackReports", "Edit")]
        public async Task<IActionResult> SaveNote([FromForm] SaveRemedialTrackReportNoteInput input, CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var actor = new RemedialTrackActor(user.Id, user.FullName ?? user.UserName ?? "SYSTEM");
            var r = await _reports.SaveReportNoteAsync(input.EnrollmentId, input.Note, actor, await ScopeAsync(), ct);
            return Json(new { success = r.Success, message = r.Message });
        }

        [HttpGet]
        [AdminPermission("RemedialTrackReports", "Read")]
        public async Task<IActionResult> Batch(int publicationId, CancellationToken ct)
        {
            var vm = await _reports.GetBatchReportAsync(publicationId, await ScopeAsync(), ct);
            if (vm is null) return NotFound();
            return View(vm);
        }

        private async Task<bool> HasEditAsync()
        {
            var auth = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
            return (await auth.AuthorizeAsync(User, QdratNew.Security.AdminPermissions.AdminPermissionPolicies.RemedialTrackReports_Edit)).Succeeded;
        }

        private async Task<RemedialTrackBatchScope> ScopeAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (_batchAccess.IsPrivilegedUser(User) || await _batchAccess.HasFullBatchesAccessAsync(userId))
                return RemedialTrackBatchScope.Unrestricted;

            var ids = await _batchAccess.GetPermittedBatchIdsAsync(userId, InstructorBatchFeature.Exams);
            return new RemedialTrackBatchScope(ids.ToHashSet());
        }
    }
}
