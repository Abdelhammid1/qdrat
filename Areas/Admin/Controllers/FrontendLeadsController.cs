using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Services.Frontend.Leads;
using QdratNew.ViewModels.Admin.FrontendLeads;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminArea")]
    public class FrontendLeadsController : Controller
    {
        private readonly IFrontendLeadAdminService _leadAdmin;

        public FrontendLeadsController(IFrontendLeadAdminService leadAdmin)
        {
            _leadAdmin = leadAdmin;
        }

        // =========================================
        // 📋 لوحة طلبات الالتحاق (RL-S5)
        // filter يُبقي الروابط القديمة صالحة: contacted / not-contacted
        // =========================================
        [HttpGet]
        [AdminPermission("Settings", "EditSettings")]
        public async Task<IActionResult> Index(string filter = "all", CancellationToken ct = default)
        {
            var vm = await _leadAdmin.GetDashboardAsync(ct);

            vm.InitialStatusFilter = filter switch
            {
                "contacted" => nameof(FrontendLeadStatus.Contacted),
                "not-contacted" => nameof(FrontendLeadStatus.New),
                _ => null
            };

            return View(vm);
        }

        // =========================================
        // 🔄 تحديث الحالة + ملاحظات الأدمن (fetch)
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Settings", "EditSettings")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateLeadStatusRequest req, CancellationToken ct)
        {
            if (req == null || !ModelState.IsValid || !Enum.IsDefined(typeof(FrontendLeadStatus), req.Status))
                return BadRequest(new { ok = false });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _leadAdmin.UpdateStatusAsync(req, userId, ct);
            if (!result.Found)
                return NotFound(new { ok = false });

            return Json(new
            {
                ok = true,
                status = req.Status.ToString(),
                contactedAt = result.ContactedAt,
                counts = result.Counts
            });
        }

        // =========================================
        // ✔ تعليم أنه تم التواصل (للتوافق)
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("Settings", "EditSettings")]
        public async Task<IActionResult> MarkContacted(int id, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _leadAdmin.MarkContactedAsync(id, userId, ct))
                return NotFound();

            return RedirectToAction(nameof(Index));
        }
    }
}
