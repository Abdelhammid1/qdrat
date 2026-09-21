using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Frontend.ProfessionalCertificates;
using QdratNew.ViewModels.Admin.ProfessionalCertificates;

namespace QdratNew.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Owner,Developer")]
public class ProfessionalCertificatesController : Controller
{
    private readonly IProfessionalCertificateService _service;
    private readonly IWebHostEnvironment _env;

    public ProfessionalCertificatesController(IProfessionalCertificateService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        var courses = await _service.GetAdminCoursesAsync();
        return View(courses);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new ProfessionalCertificateCourseFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProfessionalCertificateCourseFormViewModel model)
    {
        // استثناء حقل LogoFile من التحقق إن لم يُرفع ملف
        ModelState.Remove(nameof(model.LogoFile));
        ModelState.Remove(nameof(model.ExistingLogoPath));
        ModelState.Remove(nameof(model.RemoveLogo));

        if (!ModelState.IsValid) return View(model);
        await _service.CreateCourseAsync(model, _env.WebRootPath);
        TempData["Success"] = "تم إضافة الدورة بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _service.GetCourseFormAsync(id);
        if (vm == null) return NotFound();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProfessionalCertificateCourseFormViewModel model)
    {
        ModelState.Remove(nameof(model.LogoFile));
        ModelState.Remove(nameof(model.ExistingLogoPath));
        ModelState.Remove(nameof(model.RemoveLogo));

        if (!ModelState.IsValid) return View(model);
        var result = await _service.UpdateCourseAsync(model, _env.WebRootPath);
        if (!result) return NotFound();
        TempData["Success"] = "تم تحديث الدورة بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        await _service.ToggleCourseActiveAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHomeVisibility(int id)
    {
        await _service.ToggleCourseHomeVisibilityAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRegistration(int id)
    {
        await _service.ToggleRegistrationOpenAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
