using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Enums;
using QdratNew.Services.Frontend.CourseCollections;
using QdratNew.ViewModels.Admin.CourseCollections;

namespace QdratNew.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Owner,Developer")]
public class CourseCollectionRegistrationsController : Controller
{
    private readonly ICourseCollectionService _service;

    public CourseCollectionRegistrationsController(ICourseCollectionService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index(CourseCollectionRegistrationFilterViewModel filter)
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
    public async Task<IActionResult> UpdateStatus(CourseCollectionRegistrationUpdateStatusViewModel model)
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
        await _service.UpdateRegistrationStatusAsync(new CourseCollectionRegistrationUpdateStatusViewModel
        {
            RegistrationId = id,
            NewStatus = CourseCollectionRegistrationStatus.Contacted,
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
        await _service.UpdateRegistrationStatusAsync(new CourseCollectionRegistrationUpdateStatusViewModel
        {
            RegistrationId = id,
            NewStatus = CourseCollectionRegistrationStatus.NeedFollowUp,
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
        await _service.UpdateRegistrationStatusAsync(new CourseCollectionRegistrationUpdateStatusViewModel
        {
            RegistrationId = id,
            NewStatus = CourseCollectionRegistrationStatus.Rejected,
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
        await _service.UpdateRegistrationStatusAsync(new CourseCollectionRegistrationUpdateStatusViewModel
        {
            RegistrationId = id,
            NewStatus = CourseCollectionRegistrationStatus.Converted,
            AdminNotes = adminNotes
        }, userId, userName);
        return RedirectToAction(nameof(Index));
    }
}
