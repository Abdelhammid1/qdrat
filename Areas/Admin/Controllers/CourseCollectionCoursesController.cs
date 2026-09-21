using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Frontend.CourseCollections;
using QdratNew.ViewModels.Admin.CourseCollections;

namespace QdratNew.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Owner,Developer")]
public class CourseCollectionCoursesController : Controller
{
    private readonly ICourseCollectionService _service;
    private readonly IWebHostEnvironment _env;

    public CourseCollectionCoursesController(ICourseCollectionService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    public async Task<IActionResult> Index(int collectionId)
    {
        var collection = await _service.GetCollectionFormAsync(collectionId);
        if (collection == null) return NotFound();

        ViewBag.CollectionId = collectionId;
        ViewBag.CollectionName = collection.Name;

        var courses = await _service.GetAdminCoursesAsync(collectionId);
        return View(courses);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int collectionId)
    {
        var collection = await _service.GetCollectionFormAsync(collectionId);
        if (collection == null) return NotFound();

        ViewBag.CollectionName = collection.Name;
        return View(new CourseCollectionCourseFormViewModel { CourseCollectionId = collectionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseCollectionCourseFormViewModel model)
    {
        ModelState.Remove(nameof(model.LogoFile));
        ModelState.Remove(nameof(model.ExistingLogoPath));
        ModelState.Remove(nameof(model.RemoveLogo));

        if (!ModelState.IsValid)
        {
            var collection = await _service.GetCollectionFormAsync(model.CourseCollectionId);
            ViewBag.CollectionName = collection?.Name;
            return View(model);
        }

        await _service.CreateCourseAsync(model, _env.WebRootPath);
        TempData["Success"] = "تم إضافة الدورة بنجاح.";
        return RedirectToAction(nameof(Index), new { collectionId = model.CourseCollectionId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _service.GetCourseFormAsync(id);
        if (vm == null) return NotFound();

        var collection = await _service.GetCollectionFormAsync(vm.CourseCollectionId);
        ViewBag.CollectionName = collection?.Name;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CourseCollectionCourseFormViewModel model)
    {
        ModelState.Remove(nameof(model.LogoFile));
        ModelState.Remove(nameof(model.ExistingLogoPath));
        ModelState.Remove(nameof(model.RemoveLogo));

        if (!ModelState.IsValid)
        {
            var collection = await _service.GetCollectionFormAsync(model.CourseCollectionId);
            ViewBag.CollectionName = collection?.Name;
            return View(model);
        }

        var result = await _service.UpdateCourseAsync(model, _env.WebRootPath);
        if (!result) return NotFound();
        TempData["Success"] = "تم تحديث الدورة بنجاح.";
        return RedirectToAction(nameof(Index), new { collectionId = model.CourseCollectionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, int collectionId)
    {
        await _service.ToggleCourseActiveAsync(id);
        return RedirectToAction(nameof(Index), new { collectionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHomeVisibility(int id, int collectionId)
    {
        await _service.ToggleCourseHomeVisibilityAsync(id);
        return RedirectToAction(nameof(Index), new { collectionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRegistration(int id, int collectionId)
    {
        await _service.ToggleCourseRegistrationOpenAsync(id);
        return RedirectToAction(nameof(Index), new { collectionId });
    }
}
