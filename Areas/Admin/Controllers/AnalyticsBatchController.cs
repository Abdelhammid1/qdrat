using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Analytics;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("Admin/Analytics/[action]")]
    [Authorize(Roles = "Owner,Developer")]
    public class AnalyticsBatchController : Controller
    {
        private readonly IBatchAnalyticsService _batchService;
        private readonly IAnalyticsDashboardService _dashboardService;

        public AnalyticsBatchController(
            IBatchAnalyticsService batchService,
            IAnalyticsDashboardService dashboardService)
        {
            _batchService = batchService;
            _dashboardService = dashboardService;
        }

        [HttpGet]
        public async Task<IActionResult> BatchPerformanceDetails(
            int batchId, int? curriculumId, int? instructorId)
        {
            var vm = await _batchService.BuildBatchPerformanceAsync(batchId, curriculumId, instructorId);

            if (vm == null) return NotFound();

            return View("~/Areas/Admin/Views/Analytics/BatchPerformanceDetails.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendBatchGuidanceToInstructors(int batchId)
        {
            TempData["Success"] = "تم تجهيز توجيه مدربي الدفعة. اربط هذا الأكشن بخدمة الإشعارات لإرساله داخل حسابات المدربين.";

            return RedirectToAction("BatchPerformanceDetails", new { batchId });
        }
    }
}
