using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Analytics;
using QdratNew.Services.Analytics.Interfaces;
using QdratNew.ViewModels.Admin.Analytics;
using QdratNew.ViewModels.Admin.Shared;
using QdratNew.ViewModels.Analytics;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("Admin/Analytics/[action]")]
    [Authorize(Roles = "Owner,Developer")]
    public class AnalyticsDashboardController : Controller
    {
        private readonly IAnalyticsDashboardService _dashboardService;
        private readonly IAnalyticsSectionsService  _sectionsService;
        private readonly IMetricDecisionService _metricDecisionService;
        private readonly IQuestionDifficultyRecalibrationService _recalibrationService;

        public AnalyticsDashboardController(
            IAnalyticsDashboardService dashboardService,
            IAnalyticsSectionsService  sectionsService,
            IMetricDecisionService metricDecisionService,
            IQuestionDifficultyRecalibrationService recalibrationService)
        {
            _dashboardService      = dashboardService;
            _sectionsService       = sectionsService;
            _metricDecisionService = metricDecisionService;
            _recalibrationService  = recalibrationService;
        }

        /// <summary>
        /// Shell page — loads only filter dropdowns (instant).
        /// Content is lazy-loaded via AdvancedDashboardData AJAX call.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AdvancedDashboard(
            int? curriculumId, int? batchId, int? instructorId)
        {
            var shell = await _sectionsService.GetShellAsync(curriculumId, batchId, instructorId);
            return View("~/Areas/Admin/Views/Analytics/AdvancedDashboard.cshtml", shell);
        }

        /// <summary>
        /// AJAX content endpoint — uses SQL aggregations, no full-table scans.
        /// Returns the full dashboard partial including decision panel + KPIs + charts.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AdvancedDashboardData(
            int? curriculumId, int? batchId, int? instructorId, int page = 1)
        {
            var vm = await _sectionsService.BuildAsync(curriculumId, batchId, instructorId, page);
            return PartialView("~/Views/Shared/_AdvancedDashboardContent.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> AnalyzedStudentsDetails(
            int? curriculumId, int? batchId, int? instructorId, int page = 1)
        {
            var vm = await _sectionsService.BuildAsync(curriculumId, batchId, instructorId, page);
            return View("~/Areas/Admin/Views/Analytics/AnalyzedStudentsDetails.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> CriticalStudentsDetails(
            int? curriculumId, int? batchId, int? instructorId, int page = 1)
        {
            var vm = await _sectionsService.BuildAsync(curriculumId, batchId, instructorId, page);
            return View("~/Areas/Admin/Views/Analytics/CriticalStudentsDetails.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> HighRiskLessonsDetails(
            int? curriculumId, int? batchId, int? instructorId, int page = 1)
        {
            var vm = await _sectionsService.BuildAsync(curriculumId, batchId, instructorId, page);
            return View("~/Areas/Admin/Views/Analytics/HighRiskLessonsDetails.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> SystemRiskDetails(
            int? curriculumId, int? batchId, int? instructorId, int page = 1)
        {
            var vm = await _sectionsService.BuildAsync(curriculumId, batchId, instructorId, page);
            return View("~/Areas/Admin/Views/Analytics/SystemRiskDetails.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> MetricDecisionDetails(
            string metric, int? curriculumId, int? batchId, int? instructorId)
        {
            var dashboard = await _sectionsService.BuildAsync(curriculumId, batchId, instructorId, 1);
            var vm = _metricDecisionService.BuildMetricDecisionDetails(metric, dashboard);
            return View("~/Areas/Admin/Views/Analytics/MetricDecisionDetails.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearAnalyticsCache()
        {
            _dashboardService.InvalidateCache();
            _sectionsService.InvalidateCache();
            return Json(new { success = true });
        }

        // ── توزيع الأسئلة حسب الصعوبة ───────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetQuestionDistribution(int? curriculumId)
        {
            var data = await _recalibrationService.GetDistributionAsync(curriculumId);
            return Json(new { success = true, data });
        }

        // ── إعادة تصنيف صعوبة الأسئلة ────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewDifficultyRecalibration(
            [FromBody] QuestionDifficultyRecalibrationInputVM input)
        {
            if (input == null)
                return BadRequest(new { success = false, message = "بيانات غير صحيحة." });

            if (input.VeryHardMaxPercent < 0 || input.VeryHardMaxPercent >= input.HardMaxPercent
                || input.HardMaxPercent >= input.MediumMaxPercent || input.MediumMaxPercent >= 100)
            {
                return BadRequest(new { success = false, message = "النسب المدخلة غير منطقية. تأكد من الترتيب التصاعدي." });
            }

            var result = await _recalibrationService.PreviewAsync(input);
            return Json(new { success = true, data = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyDifficultyRecalibration(
            [FromBody] QuestionDifficultyRecalibrationInputVM input)
        {
            if (input == null)
                return BadRequest(new { success = false, message = "بيانات غير صحيحة." });

            if (input.VeryHardMaxPercent < 0 || input.VeryHardMaxPercent >= input.HardMaxPercent
                || input.HardMaxPercent >= input.MediumMaxPercent || input.MediumMaxPercent >= 100)
            {
                return BadRequest(new { success = false, message = "النسب المدخلة غير منطقية. تأكد من الترتيب التصاعدي." });
            }

            var result = await _recalibrationService.ApplyAsync(input);
            return Json(new { success = true, data = result });
        }
    }
}
