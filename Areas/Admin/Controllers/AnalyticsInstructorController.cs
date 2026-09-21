using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Analytics;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("Admin/Analytics/[action]")]
    [Authorize(Roles = "Owner,Developer")]
    public class AnalyticsInstructorController : Controller
    {
        private readonly IInstructorAnalyticsService _instructorService;

        public AnalyticsInstructorController(IInstructorAnalyticsService instructorService)
        {
            _instructorService = instructorService;
        }

        [HttpGet]
        public async Task<IActionResult> InstructorPerformanceDetails(int instructorId)
        {
            var vm = await _instructorService.BuildInstructorPerformanceAsync(instructorId);

            if (vm == null) return NotFound();

            return View("~/Areas/Admin/Views/Analytics/InstructorPerformanceDetails.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendInstructorGuidance(int instructorId, int batchId, int lessonId)
        {
            TempData["Success"] = "تم تجهيز توجيه المدرب بنجاح. اربط هذا الأكشن بخدمة الإشعارات المعتمدة لديك لإرساله لحساب المدرب.";

            return RedirectToAction("InstructorPerformanceDetails", new { instructorId });
        }
    }
}
