using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Entities;
using QdratNew.Enums;
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
        private readonly IRemedialTrackParentReportAdminService _parentAdmin;
        private readonly IEmployeeBatchAccessService _batchAccess;
        private readonly UserManager<ApplicationUser> _userManager;

        public RemedialTrackReportsController(
            IRemedialTrackReportService reports,
            IRemedialTrackParentReportAdminService parentAdmin,
            IEmployeeBatchAccessService batchAccess,
            UserManager<ApplicationUser> userManager)
        {
            _reports = reports;
            _parentAdmin = parentAdmin;
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

        // ---------------- RTK-S12.1: طابور تقارير أولياء الأمور ----------------

        [HttpGet]
        [AdminPermission("RemedialTrackReports", "Read")]
        public async Task<IActionResult> ParentReports(RemedialTrackParentReportStatus? status, int? publicationId, int? batchId, int page = 1, CancellationToken ct = default)
        {
            var filter = new RemedialTrackParentReportQueueFilter { Status = status, PublicationId = publicationId, BatchId = batchId, Page = page };
            var vm = await _parentAdmin.GetQueueAsync(filter, await ScopeAsync(), ct);
            vm.CanSend = await CanAsync(QdratNew.Security.AdminPermissions.AdminPermissionPolicies.RemedialTrackReports_Send);
            vm.CanToggleAutoSend = await CanAsync(QdratNew.Security.AdminPermissions.AdminPermissionPolicies.RemedialTrackPublications_ManageReview);
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTrackReports", "Send")]
        public Task<IActionResult> SendParentReport([FromForm] RemedialTrackParentReportActionInput input, CancellationToken ct)
            => RunReportActionAsync((actor, scope) => _parentAdmin.SendAsync(input.Id, actor, scope, ct));

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTrackReports", "Send")]
        public Task<IActionResult> ResendParentReport([FromForm] RemedialTrackParentReportActionInput input, CancellationToken ct)
            => RunReportActionAsync((actor, scope) => _parentAdmin.ResendAsync(input.Id, actor, scope, ct));

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTrackReports", "Send")]
        public Task<IActionResult> SuppressParentReport([FromForm] RemedialTrackParentReportActionInput input, CancellationToken ct)
            => RunReportActionAsync((actor, scope) => _parentAdmin.SuppressAsync(input.Id, actor, scope, ct));

        private async Task<IActionResult> RunReportActionAsync(
            Func<RemedialTrackActor, RemedialTrackBatchScope, Task<RemedialTrackParentReportActionResult>> action)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var actor = new RemedialTrackActor(user.Id, user.FullName ?? user.UserName ?? "SYSTEM");
            var r = await action(actor, await ScopeAsync());
            if (!r.Found) return NotFound();   // خارج نطاق الدفعات ≡ غير موجود (لا تسريب)
            return Json(new { success = r.Success, message = r.Message });
        }

        private async Task<bool> CanAsync(string policy)
        {
            var auth = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
            return (await auth.AuthorizeAsync(User, policy)).Succeeded;
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
