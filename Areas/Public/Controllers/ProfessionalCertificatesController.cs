using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Frontend.ProfessionalCertificates;
using QdratNew.ViewModels.Frontend.ProfessionalCertificates;

namespace QdratNew.Areas.Public.Controllers;

[Area("Public")]
public class ProfessionalCertificatesController : Controller
{
    private readonly IProfessionalCertificateService _service;

    public ProfessionalCertificatesController(IProfessionalCertificateService service)
    {
        _service = service;
    }

    [HttpGet]
    [Route("Public/ProfessionalCertificates/Register/{slug}")]
    public async Task<IActionResult> Register(string slug)
    {
        var vm = await _service.GetRegisterViewModelAsync(slug);
        if (vm == null) return NotFound();
        return View(vm);
    }

    [HttpPost]
    [Route("Public/ProfessionalCertificates/Register/{slug}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string slug, ProfessionalCertificateRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var courseVm = await _service.GetRegisterViewModelAsync(slug);
            if (courseVm == null) return NotFound();
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
            var courseVm = await _service.GetRegisterViewModelAsync(slug);
            if (courseVm == null) return NotFound();
            model.CourseTitleAr = courseVm.CourseTitleAr;
            model.StandardCode = courseVm.StandardCode;
            model.CourseShortDescription = courseVm.CourseShortDescription;
            model.CourseFullDescription = courseVm.CourseFullDescription;
            model.LogoPath = courseVm.LogoPath;
            ModelState.AddModelError(string.Empty, errorMessage ?? "حدث خطأ غير متوقع.");
            return View(model);
        }

        return RedirectToAction(nameof(Thanks), new { id = registrationId });
    }

    [HttpGet]
    [Route("Public/ProfessionalCertificates/Thanks/{id:int}")]
    public async Task<IActionResult> Thanks(int id)
    {
        var vm = await _service.GetThanksAsync(id);
        if (vm == null) return NotFound();
        return View(vm);
    }

    // ── Multi-course registration ──────────────────────────────

    [HttpGet]
    [Route("Public/ProfessionalCertificates/Register")]
    public async Task<IActionResult> MultiRegister()
    {
        var vm = await _service.GetMultiRegisterViewModelAsync();
        return View(vm);
    }

    [HttpPost]
    [Route("Public/ProfessionalCertificates/Register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MultiRegister(ProfessionalCertificateMultiRegisterViewModel model)
    {
        ModelState.Remove("Courses");

        if (!ModelState.IsValid)
        {
            var fresh = await _service.GetMultiRegisterViewModelAsync();
            model.Courses = fresh.Courses;
            return View(model);
        }

        var ip        = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();

        var (success, errorMessage, registeredCourses) =
            await _service.CreateMultiRegistrationAsync(model, ip, userAgent);

        if (!success)
        {
            var fresh = await _service.GetMultiRegisterViewModelAsync();
            model.Courses = fresh.Courses;
            ModelState.AddModelError(string.Empty, errorMessage ?? "حدث خطأ غير متوقع.");
            return View(model);
        }

        TempData["RegisteredCourses"] = System.Text.Json.JsonSerializer.Serialize(registeredCourses);
        TempData["RegisteredName"]    = model.FullName;
        return RedirectToAction(nameof(MultiThanks));
    }

    [HttpGet]
    [Route("Public/ProfessionalCertificates/Thanks")]
    public IActionResult MultiThanks()
    {
        var courses = TempData["RegisteredCourses"] as string;
        var name    = TempData["RegisteredName"] as string;

        if (string.IsNullOrEmpty(courses)) return RedirectToAction(nameof(MultiRegister));

        var vm = new ProfessionalCertificateMultiThanksViewModel
        {
            FullName            = name ?? string.Empty,
            RegisteredCourseNames = System.Text.Json.JsonSerializer
                                        .Deserialize<List<string>>(courses) ?? new(),
            SubmittedAt = DateTime.UtcNow
        };

        return View(vm);
    }
}
