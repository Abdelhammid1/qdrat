using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Frontend.CourseCollections;
using QdratNew.ViewModels.Admin.CourseCollections;

namespace QdratNew.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Owner,Developer")]
public class CourseCollectionsController : Controller
{
    private readonly ICourseCollectionService _service;
    private readonly IWebHostEnvironment _env;

    public CourseCollectionsController(ICourseCollectionService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        var collections = await _service.GetAdminCollectionsAsync();
        return View(collections);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CourseCollectionFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseCollectionFormViewModel model)
    {
        ModelState.Remove(nameof(model.LogoFile));
        ModelState.Remove(nameof(model.ExistingLogoPath));
        ModelState.Remove(nameof(model.RemoveLogo));
        ModelState.Remove(nameof(model.BackgroundImageFile));
        ModelState.Remove(nameof(model.ExistingBackgroundImagePath));
        ModelState.Remove(nameof(model.RemoveBackgroundImage));

        if (!ModelState.IsValid) return View(model);
        await _service.CreateCollectionAsync(model, _env.WebRootPath);
        TempData["Success"] = "تم إضافة المجموعة بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _service.GetCollectionFormAsync(id);
        if (vm == null) return NotFound();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CourseCollectionFormViewModel model)
    {
        ModelState.Remove(nameof(model.LogoFile));
        ModelState.Remove(nameof(model.ExistingLogoPath));
        ModelState.Remove(nameof(model.RemoveLogo));
        ModelState.Remove(nameof(model.BackgroundImageFile));
        ModelState.Remove(nameof(model.ExistingBackgroundImagePath));
        ModelState.Remove(nameof(model.RemoveBackgroundImage));

        if (!ModelState.IsValid) return View(model);
        var result = await _service.UpdateCollectionAsync(model, _env.WebRootPath);
        if (!result) return NotFound();
        TempData["Success"] = "تم تحديث المجموعة بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        await _service.ToggleCollectionActiveAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHomeVisibility(int id)
    {
        await _service.ToggleCollectionHomeVisibilityAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
