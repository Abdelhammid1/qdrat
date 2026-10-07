using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QdratNew.Areas.Parents.Controllers.Base;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.Services.RemedialTracks;

namespace QdratNew.Areas.Parents.Controllers
{
    /// <summary>
    /// RTK-S11.3 (D28): تقارير الخطة العلاجية لأبناء ولي الأمر. المتحكّم رفيع: ولي الأمر الحالي من الهوية
    /// (ParentBaseController) ويُمرَّر للخدمة؛ لا يُقبل معرّف ولي أمر أو طالب من العميل. غير المملوك ← 404.
    /// </summary>
    public class RemedialTrackReportsController : ParentBaseController
    {
        private const string MessageKey = "RtkParentReportMessage";
        private readonly IRemedialTrackParentReportService _reports;

        public RemedialTrackReportsController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IParentAccessService parentAccessService,
            IRemedialTrackParentReportService reports)
            : base(contextFactory, userManager, parentAccessService)
        {
            _reports = reports;
        }

        [HttpGet]
        [EnableRateLimiting("rtk-parent-report")]
        public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
        {
            var vm = await _reports.GetListAsync(ParentId, page, ct);
            return View(vm);
        }

        [HttpGet]
        [EnableRateLimiting("rtk-parent-report")]
        public async Task<IActionResult> Details(int id, CancellationToken ct = default)
        {
            var vm = await _reports.GetDetailsAsync(ParentId, id, ct);
            if (vm is null) return NotFound();
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("rtk-parent-report")]
        public async Task<IActionResult> Acknowledge(int id, CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            var result = await _reports.AcknowledgeAsync(ParentId, id, user?.Id, user?.FullName ?? user?.UserName, ct);
            if (!result.Found) return NotFound();

            TempData[MessageKey] = result.AlreadyAcknowledged
                ? "سبق تسجيل إقرارك بالاطلاع على هذا التقرير."
                : "تم تسجيل إقرارك بالاطلاع على التقرير. شكرًا لمتابعتكم.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
