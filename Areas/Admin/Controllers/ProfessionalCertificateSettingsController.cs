using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Frontend.ProfessionalCertificates;
using QdratNew.ViewModels.Admin.ProfessionalCertificates;

namespace QdratNew.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Owner,Developer")]
public class ProfessionalCertificateSettingsController : Controller
{
    private readonly IProfessionalCertificateService _service;

    public ProfessionalCertificateSettingsController(IProfessionalCertificateService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var vm = await _service.GetSectionSettingAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProfessionalCertificateSectionSettingViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        await _service.UpdateSectionSettingAsync(model, userId);
        TempData["Success"] = "تم حفظ الإعدادات بنجاح.";
        return RedirectToAction(nameof(Edit));
    }
}
