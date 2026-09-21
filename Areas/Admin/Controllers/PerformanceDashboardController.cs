using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Security;
using QdratNew.Services.Interfaces;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PerformanceDashboardController : Controller
    {
        private readonly IPerformanceDashboardService _service;

        public PerformanceDashboardController(IPerformanceDashboardService service)
        {
            _service = service;
        }

        // ✅ الصفحة الرئيسية للوحة المؤشرات
        [HttpGet]
        [AdminPermission("PerformanceDashboard", "Read")]
        public async Task<IActionResult> Index()
        {
            var vm = await _service.GetDashboardDataAsync();
            vm.ActiveBatches = await _service.GetActiveBatchPerformanceAsync(); // ✅ هنا الإضافة
            return View(vm);
        }

        [HttpGet]
        [AdminPermission("PerformanceDashboard", "ExamAnalytics")]
        public async Task<IActionResult> StudentRemedialDetails(int studentId, int examId)
        {
            var vm = await _service.BuildStudentRemedialAnalysisAsync(studentId, examId);
            if (vm == null)
                return NotFound("لم يتم العثور على بيانات الطالب أو التحليل المطلوب.");

            return View(vm);
        }




        // ✅ عرض تفاصيل دفعة معينة
        [HttpGet]
        [AdminPermission("PerformanceDashboard", "BatchDetails")]
        public async Task<IActionResult> BatchDetails(int batchId)
        {
            var vm = await _service.GetBatchPerformanceDetailsAsync(batchId);
            return PartialView("_BatchPerformanceDetailPartial", vm);
        }

        [HttpGet]
        [AdminPermission("PerformanceDashboard", "ExamReport")]
        public async Task<IActionResult> ExamReport(int examId)
        {
            var vm = await _service.GetExamPerformanceDetailsAsync(examId);

            if (vm == null)
                return Content("<div class='alert alert-warning text-center'>⚠️ لم يتم العثور على بيانات هذا الاختبار.</div>", "text/html");

            return PartialView("_ExamPerformancePartial", vm);
        }


        [HttpGet]
        [AdminPermission("PerformanceDashboard", "ExamAnalytics")]
        public async Task<IActionResult> ExamAnalytics(int examId)
        {
            var vm = await _service.GetExamAnalyticsAsync(examId);
            if (vm == null)
                return Content("<div class='alert alert-warning text-center'>⚠️ لا توجد بيانات تحليلية لهذا الاختبار.</div>", "text/html");

            return PartialView("_ExamAnalyticsPartial", vm);
        }



        [HttpGet]
        [AdminPermission("PerformanceDashboard", "BatchDetails")]
        public async Task<IActionResult> LoadBatchExams(int batchId)
        {
            var exams = await _service.GetExamsForBatchAsync(batchId);
            return PartialView("_BatchExamsPartial", exams);
        }



        // ✅ API لتحميل تفاصيل الدفعة (مستخدم من JS)
        [HttpGet]
        [AdminPermission("PerformanceDashboard", "BatchDetails")]
        public async Task<IActionResult> LoadBatchSummaryData(int batchId)
        {
            var vm = await _service.GetBatchPerformanceDetailsAsync(batchId);
            return Json(vm);
        }
    }
}
