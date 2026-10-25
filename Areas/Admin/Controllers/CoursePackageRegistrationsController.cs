using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Services.Frontend.Packages;
using QdratNew.ViewModels.Admin.CoursePackages;

namespace QdratNew.Areas.Admin.Controllers;

// PKG-S5.3 — صندوق طلبات حجز الباقات. نفس صلاحية صندوق FrontendLeads (لا وحدة صلاحيات جديدة).
[Area("Admin")]
[Authorize(Policy = "AdminArea")]
public class CoursePackageRegistrationsController : Controller
{
    private readonly ICoursePackageRegistrationAdminService _service;

    public CoursePackageRegistrationsController(ICoursePackageRegistrationAdminService service)
    {
        _service = service;
    }

    [HttpGet]
    [AdminPermission("Settings", "EditSettings")]
    public async Task<IActionResult> Index(CoursePackageRegistrationFilter filter, CancellationToken ct)
    {
        var vm = await _service.GetIndexAsync(filter ?? new CoursePackageRegistrationFilter(), ct);
        return View(vm);
    }

    [HttpGet]
    [AdminPermission("Settings", "EditSettings")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var vm = await _service.GetDetailsAsync(id, ct);
        if (vm == null) return NotFound();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AdminPermission("Settings", "EditSettings")]
    public async Task<IActionResult> UpdateStatus(UpdateCoursePackageRegistrationRequest request, CancellationToken ct)
    {
        if (request == null || !Enum.IsDefined(typeof(CoursePackageRegistrationStatus), request.Status))
            return BadRequest();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "تعذّر الحفظ: الملاحظات يجب ألا تتجاوز 1000 حرف.";
            return RedirectToAction(nameof(Details), new { id = request.Id });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!await _service.UpdateAsync(request, userId, ct))
            return NotFound();

        TempData["Success"] = "تم حفظ حالة الطلب والملاحظات.";
        return RedirectToAction(nameof(Details), new { id = request.Id });
    }
}
