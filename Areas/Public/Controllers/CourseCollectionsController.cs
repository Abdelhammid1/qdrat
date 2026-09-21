using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Frontend.CourseCollections;
using QdratNew.ViewModels.Frontend.CourseCollections;

namespace QdratNew.Areas.Public.Controllers;

[Area("Public")]
public class CourseCollectionsController : Controller
{
    private readonly ICourseCollectionService _service;

    public CourseCollectionsController(ICourseCollectionService service)
    {
        _service = service;
    }

    [HttpGet]
    [Route("Public/CourseCollections/{collectionSlug}/Register/{courseSlug}")]
    public async Task<IActionResult> Register(string collectionSlug, string courseSlug)
    {
        var vm = await _service.GetRegisterViewModelAsync(collectionSlug, courseSlug);
        if (vm == null) return NotFound();
        return View(vm);
    }

    [HttpPost]
    [Route("Public/CourseCollections/{collectionSlug}/Register/{courseSlug}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string collectionSlug, string courseSlug, CourseCollectionRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var courseVm = await _service.GetRegisterViewModelAsync(collectionSlug, courseSlug);
            if (courseVm == null) return NotFound();
            model.CollectionSlug = courseVm.CollectionSlug;
            model.CollectionName = courseVm.CollectionName;
            model.CourseId = courseVm.CourseId;
            model.CourseTitleAr = courseVm.CourseTitleAr;
            model.StandardCode = courseVm.StandardCode;
            model.CourseShortDescription = courseVm.CourseShortDescription;
            model.CourseFullDescription = courseVm.CourseFullDescription;
            model.LogoPath = courseVm.LogoPath;
            return View(model);
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();

        var (success, registrationId, errorMessage) = await _service.CreateRegistrationAsync(model, ip, userAgent);

        if (!success)
        {
            var courseVm = await _service.GetRegisterViewModelAsync(collectionSlug, courseSlug);
            if (courseVm == null) return NotFound();
            model.CollectionSlug = courseVm.CollectionSlug;
            model.CollectionName = courseVm.CollectionName;
            model.CourseTitleAr = courseVm.CourseTitleAr;
            model.StandardCode = courseVm.StandardCode;
            model.CourseShortDescription = courseVm.CourseShortDescription;
            model.CourseFullDescription = courseVm.CourseFullDescription;
            model.LogoPath = courseVm.LogoPath;
            ModelState.AddModelError(string.Empty, errorMessage ?? "حدث خطأ غير متوقع.");
            return View(model);
        }

        return RedirectToAction(nameof(Thanks), new { collectionSlug, id = registrationId });
    }

    [HttpGet]
    [Route("Public/CourseCollections/{collectionSlug}/Thanks/{id:int}")]
    public async Task<IActionResult> Thanks(string collectionSlug, int id)
    {
        var vm = await _service.GetThanksAsync(id);
        if (vm == null) return NotFound();
        return View(vm);
    }

    // ── Multi-course registration ──────────────────────────────

    [HttpGet]
    [Route("Public/CourseCollections/{collectionSlug}/Register")]
    public async Task<IActionResult> MultiRegister(string collectionSlug)
    {
        var vm = await _service.GetMultiRegisterViewModelAsync(collectionSlug);
        if (vm == null) return NotFound();
        return View(vm);
    }

    [HttpPost]
    [Route("Public/CourseCollections/{collectionSlug}/Register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MultiRegister(string collectionSlug, CourseCollectionMultiRegisterViewModel model)
    {
        ModelState.Remove("Courses");

        if (!ModelState.IsValid)
        {
            var fresh = await _service.GetMultiRegisterViewModelAsync(collectionSlug);
            if (fresh == null) return NotFound();
            model.Courses = fresh.Courses;
            model.CollectionSlug = fresh.CollectionSlug;
            model.CollectionName = fresh.CollectionName;
            return View(model);
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();

        var (success, errorMessage, registeredCourses) =
            await _service.CreateMultiRegistrationAsync(collectionSlug, model, ip, userAgent);

        if (!success)
        {
            var fresh = await _service.GetMultiRegisterViewModelAsync(collectionSlug);
            if (fresh == null) return NotFound();
            model.Courses = fresh.Courses;
            model.CollectionSlug = fresh.CollectionSlug;
            model.CollectionName = fresh.CollectionName;
            ModelState.AddModelError(string.Empty, errorMessage ?? "حدث خطأ غير متوقع.");
            return View(model);
        }

        TempData["RegisteredCourses"] = System.Text.Json.JsonSerializer.Serialize(registeredCourses);
        TempData["RegisteredName"] = model.FullName;
        TempData["RegisteredCollectionName"] = model.CollectionName;
        return RedirectToAction(nameof(MultiThanks), new { collectionSlug });
    }

    [HttpGet]
    [Route("Public/CourseCollections/{collectionSlug}/Thanks")]
    public IActionResult MultiThanks(string collectionSlug)
    {
        var courses = TempData["RegisteredCourses"] as string;
        var name = TempData["RegisteredName"] as string;
        var collectionName = TempData["RegisteredCollectionName"] as string;

        if (string.IsNullOrEmpty(courses)) return RedirectToAction(nameof(MultiRegister), new { collectionSlug });

        var vm = new CourseCollectionMultiThanksViewModel
        {
            CollectionSlug = collectionSlug,
            CollectionName = collectionName ?? string.Empty,
            FullName = name ?? string.Empty,
            RegisteredCourseNames = System.Text.Json.JsonSerializer
                                        .Deserialize<List<string>>(courses) ?? new(),
            SubmittedAt = DateTime.Now
        };

        return View(vm);
    }
}
