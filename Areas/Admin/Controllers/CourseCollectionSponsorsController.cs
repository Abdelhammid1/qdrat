using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Frontend.CourseCollections;
using QdratNew.ViewModels.Admin.CourseCollections;

namespace QdratNew.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Owner,Developer")]
public class CourseCollectionSponsorsController : Controller
{
    private readonly ICourseCollectionService _service;
    private readonly IWebHostEnvironment _env;

    public CourseCollectionSponsorsController(ICourseCollectionService service, IWebHostEnvironment env)
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

        var sponsors = await _service.GetAdminSponsorsAsync(collectionId);
        return View(sponsors);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int collectionId)
    {
        var collection = await _service.GetCollectionFormAsync(collectionId);
        if (collection == null) return NotFound();

        ViewBag.CollectionName = collection.Name;
        return View(new CourseCollectionSponsorFormViewModel { CourseCollectionId = collectionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseCollectionSponsorFormViewModel model)
    {
        ModelState.Remove(nameof(model.ExistingLogoPath));

        if (model.LogoFile == null || model.LogoFile.Length == 0)
            ModelState.AddModelError(nameof(model.LogoFile), "لوجو الراعي مطلوب.");

        if (!ModelState.IsValid)
        {
            var collection = await _service.GetCollectionFormAsync(model.CourseCollectionId);
            ViewBag.CollectionName = collection?.Name;
            return View(model);
        }

        await _service.CreateSponsorAsync(model, _env.WebRootPath);
        TempData["Success"] = "تم إضافة الراعي بنجاح.";
        return RedirectToAction(nameof(Index), new { collectionId = model.CourseCollectionId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, int collectionId)
    {
        var collection = await _service.GetCollectionFormAsync(collectionId);
        if (collection == null) return NotFound();

        var sponsor = (await _service.GetAdminSponsorsAsync(collectionId)).FirstOrDefault(s => s.Id == id);
        if (sponsor == null) return NotFound();

        ViewBag.CollectionName = collection.Name;
        return View(new CourseCollectionSponsorFormViewModel
        {
            Id = sponsor.Id,
            CourseCollectionId = sponsor.CourseCollectionId,
            Name = sponsor.Name,
            LinkUrl = sponsor.LinkUrl,
            ShowOnHomePage = sponsor.ShowOnHomePage,
            DisplayOrder = sponsor.DisplayOrder,
            ExistingLogoPath = sponsor.LogoPath
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CourseCollectionSponsorFormViewModel model)
    {
        ModelState.Remove(nameof(model.ExistingLogoPath));

        if (!ModelState.IsValid)
        {
            var collection = await _service.GetCollectionFormAsync(model.CourseCollectionId);
            ViewBag.CollectionName = collection?.Name;
            return View(model);
        }

        var result = await _service.UpdateSponsorAsync(model, _env.WebRootPath);
        if (!result) return NotFound();
        TempData["Success"] = "تم تحديث الراعي بنجاح.";
        return RedirectToAction(nameof(Index), new { collectionId = model.CourseCollectionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHomeVisibility(int id, int collectionId)
    {
        await _service.ToggleSponsorHomeVisibilityAsync(id);
        return RedirectToAction(nameof(Index), new { collectionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int collectionId)
    {
        await _service.DeleteSponsorAsync(id, _env.WebRootPath);
        TempData["Success"] = "تم حذف الراعي بنجاح.";
        return RedirectToAction(nameof(Index), new { collectionId });
    }
}
