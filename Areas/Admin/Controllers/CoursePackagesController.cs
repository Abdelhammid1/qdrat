using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Frontend.Packages;
using QdratNew.ViewModels.Admin.CoursePackages;

namespace QdratNew.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Owner,Developer")]
public class CoursePackagesController : Controller
{
    private readonly ICoursePackageAdminService _service;
    private readonly IWebHostEnvironment _env;

    public CoursePackagesController(ICoursePackageAdminService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var packages = await _service.GetListAsync(ct);
        return View(packages);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CoursePackageFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CoursePackageFormViewModel model, CancellationToken ct)
    {
        RemoveOptionalFileState(model);
        if (!ModelState.IsValid) return View(model);

        var (ok, id, errors) = await _service.CreateAsync(model, _env.WebRootPath, CurrentUserId(), ct);
        if (!ok)
        {
            AddErrors(errors);
            return View(model);
        }

        TempData["Success"] = "تم إضافة الباقة بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var vm = await _service.GetFormAsync(id, ct);
        if (vm == null) return NotFound();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CoursePackageFormViewModel model, CancellationToken ct)
    {
        RemoveOptionalFileState(model);
        if (!ModelState.IsValid) return await ReturnFormKeepingPosterAsync(model, ct);

        var (found, errors) = await _service.UpdateAsync(model, _env.WebRootPath, CurrentUserId(), ct);
        if (!found) return NotFound();
        if (errors.Count > 0)
        {
            AddErrors(errors);
            return await ReturnFormKeepingPosterAsync(model, ct);
        }

        TempData["Success"] = "تم تحديث الباقة بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken ct)
    {
        await _service.ToggleActiveAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHomeVisibility(int id, CancellationToken ct)
    {
        await _service.ToggleHomeVisibilityAsync(id, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, _env.WebRootPath, ct);
        switch (result)
        {
            case DeletePackageResult.Deleted:
                TempData["Success"] = "تم حذف الباقة.";
                break;
            case DeletePackageResult.HasRegistrations:
                TempData["Error"] = "لا يمكن حذف باقة لها طلبات حجز. عطّلها بدلًا من حذفها.";
                break;
            default:
                return NotFound();
        }
        return RedirectToAction(nameof(Index));
    }

    // ── Helpers ─────────────────────────────────────────────────

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // خصائص الصورة اختيارية: لا تُسقط الـ ModelState بسببها (نفس نمط CourseCollectionsController)
    private void RemoveOptionalFileState(CoursePackageFormViewModel model)
    {
        ModelState.Remove(nameof(model.PosterFile));
        ModelState.Remove(nameof(model.ExistingPosterPath));
        ModelState.Remove(nameof(model.RemovePoster));
    }

    private void AddErrors(Dictionary<string, string> errors)
    {
        foreach (var (key, message) in errors)
            ModelState.AddModelError(key, message);
    }

    // عند فشل التحقق لا تُفقد الصورة الموجودة: نعيد مسارها من القاعدة (الحقل لا يُقبل من الزائر)
    private async Task<IActionResult> ReturnFormKeepingPosterAsync(CoursePackageFormViewModel model, CancellationToken ct)
    {
        var current = await _service.GetFormAsync(model.Id, ct);
        if (current == null) return NotFound();
        model.ExistingPosterPath = current.ExistingPosterPath;
        return View(model);
    }
}
