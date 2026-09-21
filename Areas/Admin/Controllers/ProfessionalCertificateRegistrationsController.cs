using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Enums;
using QdratNew.Services.Frontend.ProfessionalCertificates;
using QdratNew.ViewModels.Admin.ProfessionalCertificates;

namespace QdratNew.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Owner,Developer")]
public class ProfessionalCertificateRegistrationsController : Controller
{
    private readonly IProfessionalCertificateService _service;

    public ProfessionalCertificateRegistrationsController(IProfessionalCertificateService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index(ProfessionalCertificateRegistrationFilterViewModel filter)
    {
        var vm = await _service.GetAdminDashboardAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var vm = await _service.GetRegistrationDetailsAsync(id);
        if (vm == null) return NotFound();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(ProfessionalCertificateRegistrationUpdateStatusViewModel model)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var userName = User.Identity?.Name ?? string.Empty;
        await _service.UpdateRegistrationStatusAsync(model, userId, userName);
        TempData["Success"] = "تم تحديث الحالة بنجاح.";
        return RedirectToAction(nameof(Details), new { id = model.RegistrationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkContacted(int id, string? adminNotes)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var userName = User.Identity?.Name ?? string.Empty;
        await _service.UpdateRegistrationStatusAsync(new ProfessionalCertificateRegistrationUpdateStatusViewModel
        {
            RegistrationId = id,
            NewStatus = ProfessionalCertificateRegistrationStatus.Contacted,
            AdminNotes = adminNotes
        }, userId, userName);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNeedFollowUp(int id, string? adminNotes)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var userName = User.Identity?.Name ?? string.Empty;
        await _service.UpdateRegistrationStatusAsync(new ProfessionalCertificateRegistrationUpdateStatusViewModel
        {
            RegistrationId = id,
            NewStatus = ProfessionalCertificateRegistrationStatus.NeedFollowUp,
            AdminNotes = adminNotes
        }, userId, userName);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? rejectionReason, string? adminNotes)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var userName = User.Identity?.Name ?? string.Empty;
        await _service.UpdateRegistrationStatusAsync(new ProfessionalCertificateRegistrationUpdateStatusViewModel
        {
            RegistrationId = id,
            NewStatus = ProfessionalCertificateRegistrationStatus.Rejected,
            RejectionReason = rejectionReason,
            AdminNotes = adminNotes
        }, userId, userName);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int id, string? adminNotes)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var userName = User.Identity?.Name ?? string.Empty;
        await _service.UpdateRegistrationStatusAsync(new ProfessionalCertificateRegistrationUpdateStatusViewModel
        {
            RegistrationId = id,
            NewStatus = ProfessionalCertificateRegistrationStatus.Converted,
            AdminNotes = adminNotes
        }, userId, userName);
        return RedirectToAction(nameof(Index));
    }
}
