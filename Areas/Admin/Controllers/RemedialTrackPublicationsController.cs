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
    /// الخطة العلاجية العاجلة — النشر والمتابعة (RTK-S3). المنطق كله في IRemedialTrackPublicationService؛ الكنترولر رفيع.
    /// الدفعات المسموحة للموظف تُحسب هنا (IEmployeeBatchAccessService) وتُمرَّر للخدمة كنطاق وتُفرض داخلها.
    /// طلبات Json (fetch) تعيد دائمًا { success, message, data, warnings }.
    /// </summary>
    [Area("Admin")]
    public class RemedialTrackPublicationsController : Controller
    {
        private readonly IRemedialTrackPublicationService _publications;
        private readonly IRemedialTrackReportService _reports;
        private readonly IEmployeeBatchAccessService _batchAccess;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthorizationService _authorization;

        public RemedialTrackPublicationsController(
            IRemedialTrackPublicationService publications,
            IRemedialTrackReportService reports,
            IEmployeeBatchAccessService batchAccess,
            UserManager<ApplicationUser> userManager,
            IAuthorizationService authorization)
        {
            _publications = publications;
            _reports = reports;
            _batchAccess = batchAccess;
            _userManager = userManager;
            _authorization = authorization;
        }

        // ---------------- القائمة ----------------

        [HttpGet]
        [AdminPermission("RemedialTrackPublications", "Read")]
        public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
        {
            var vm = await _publications.GetIndexAsync(page, await ScopeAsync(), ct);
            vm.CanPublish = await CanAsync(AdminPermissionPolicies.RemedialTrackPublications_Publish);
            return View(vm);
        }

        // ---------------- النشر ----------------

        [HttpGet]
        [AdminPermission("RemedialTrackPublications", "Publish")]
        public async Task<IActionResult> Create(int? trackId, CancellationToken ct)
            => View(await _publications.GetPublishFormAsync(trackId, await ScopeAsync(), ct));

        [HttpGet]
        [AdminPermission("RemedialTrackPublications", "Publish")]
        public async Task<IActionResult> StudentsOfBatch(int batchId, string? q, int page = 1, CancellationToken ct = default)
        {
            var result = await _publications.GetStudentsOfBatchAsync(batchId, q, page, await ScopeAsync(), ct);
            if (result is null)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return Json(new { success = false, message = "🚫 الدفعة غير متاحة لك.", data = (object?)null });
            }

            return Json(new
            {
                success = true,
                message = string.Empty,
                data = new
                {
                    items = result.Items.Select(s => new { id = s.Id, name = s.Name }),
                    page = result.Page,
                    totalPages = result.TotalPages,
                    total = result.Total
                }
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTrackPublications", "Publish")]
        public async Task<IActionResult> Create([FromForm] CreateRemedialTrackPublicationInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return ValidationFail();

            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var result = await _publications.CreateAsync(input, actor, await ScopeAsync(), ct);
            return Json(new
            {
                success = result.Success,
                message = result.Message,
                data = result.Success && result.Data is RemedialTrackPublicationCreated created
                    ? new
                    {
                        id = created.PublicationId,
                        enrolled = created.Enrolled,
                        skipped = created.Skipped,
                        url = Url.Action(nameof(Details), "RemedialTrackPublications", new { area = "Admin", id = created.PublicationId })
                    }
                    : null,
                warnings = result.Warnings
            });
        }

        // ---------------- التفاصيل ----------------

        [HttpGet]
        [AdminPermission("RemedialTrackPublications", "Read")]
        public async Task<IActionResult> Details(int id, RemedialTrackEnrollmentStatus? status, bool blockedOnly = false, string? q = null, int page = 1, CancellationToken ct = default)
        {
            var canManageCode = await CanAsync(AdminPermissionPolicies.RemedialTrackPublications_ManageCode);
            var vm = await _publications.GetDetailsAsync(id, canManageCode, await ScopeAsync(), ct);
            if (vm is null)
                return NotFound();

            vm.CanManageCode = canManageCode;
            vm.CanCancel = await CanAsync(AdminPermissionPolicies.RemedialTrackPublications_Cancel);

            // RTK-S6.2: لوحة المتابعة
            var filter = new RemedialTrackDashboardFilter
            {
                Status = status.HasValue && Enum.IsDefined(typeof(RemedialTrackEnrollmentStatus), status.Value) ? status : null,
                BlockedOnly = blockedOnly,
                Q = q,
                Page = page
            };
            vm.Dashboard = await _reports.GetDashboardAsync(id, filter, await ScopeAsync(), ct);
            if (vm.Dashboard is not null)
            {
                vm.Dashboard.CanUnlock = await CanAsync(AdminPermissionPolicies.RemedialTrackPublications_Unlock);
                vm.Dashboard.CanReadReports = await CanAsync(AdminPermissionPolicies.RemedialTrackReports_Read);
            }
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTrackPublications", "ManageCode")]
        public async Task<IActionResult> RegenerateCode([FromForm] int id, CancellationToken ct)
        {
            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var r = await _publications.RegenerateCodeAsync(id, actor, await ScopeAsync(), ct);
            return Json(new { success = r.Success, message = r.Message, data = r.Data, warnings = r.Warnings });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTrackPublications", "Cancel")]
        public async Task<IActionResult> Cancel([FromForm] int id, [FromForm] string? reason, CancellationToken ct)
        {
            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var r = await _publications.CancelAsync(id, reason, actor, await ScopeAsync(), ct);
            return Json(new { success = r.Success, message = r.Message, data = r.Data, warnings = r.Warnings });
        }

        // ---------------- RTK-S6.3: فتح المحور التالي ----------------

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTrackPublications", "Unlock")]
        public async Task<IActionResult> Unlock([FromForm] UnlockRemedialTrackAxisInput input, CancellationToken ct)
        {
            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var r = await _reports.UnlockNextAxisAsync(input.EnrollmentId, input.AxisProgressId, input.Reason, actor, await ScopeAsync(), ct);
            return Json(new { success = r.Success, message = r.Message, data = r.Data, warnings = r.Warnings });
        }

        // ---------------- مساعدات ----------------

        private IActionResult ValidationFail()
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .Take(3)
                .ToList();

            var message = errors.Count == 0 ? "⚠️ البيانات غير مكتملة." : "⚠️ " + string.Join(" • ", errors);
            return Json(new { success = false, message, data = (object?)null, warnings = (object?)null });
        }

        /// <summary>نفس منطق تقييد الموظف في بقية الشاشات: المالك/المبرمج أو Batches كاملة = بلا قيود، وإلا دفعات ميزة الاختبارات.</summary>
        private async Task<RemedialTrackBatchScope> ScopeAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (_batchAccess.IsPrivilegedUser(User) || await _batchAccess.HasFullBatchesAccessAsync(userId))
                return RemedialTrackBatchScope.Unrestricted;

            var ids = await _batchAccess.GetPermittedBatchIdsAsync(userId, InstructorBatchFeature.Exams);
            return new RemedialTrackBatchScope(ids.ToHashSet());
        }

        private async Task<bool> CanAsync(string policy)
            => (await _authorization.AuthorizeAsync(User, policy)).Succeeded;

        private async Task<RemedialTrackActor?> ActorAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user is null
                ? null
                : new RemedialTrackActor(user.Id, user.FullName ?? user.UserName ?? "SYSTEM");
        }
    }
}
