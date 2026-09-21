using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.Reports.Interfaces;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    [Authorize(Roles = "Partner")]
    public class ReportsController : Controller
    {
        private readonly IBatchReportService _batchReportService;
        private readonly IStudentBatchReportService _studentBatchReportService;

        public ReportsController(
            IBatchReportService batchReportService,
            IStudentBatchReportService studentBatchReportService)
        {
            _batchReportService = batchReportService;
            _studentBatchReportService = studentBatchReportService;
        }

        // =====================================================
        // تقرير الدفعة (ملخص عام)
        // =====================================================
        [HttpGet]
        public IActionResult Batch(int batchId)
        {
            if (batchId <= 0)
                return NotFound();

            var model = _batchReportService.GetBatchReport(batchId);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =====================================================
        // تقرير الواجبات داخل الدفعة
        // =====================================================
        [HttpGet]
        public IActionResult BatchHomeworks(int batchId)
        {
            if (batchId <= 0)
                return NotFound();

            var model = _batchReportService.GetBatchHomeworkReport(batchId);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =====================================================
        // تقرير الاختبارات داخل الدفعة
        // =====================================================
        [HttpGet]
        public IActionResult BatchExams(int batchId)
        {
            if (batchId <= 0)
                return NotFound();

            var model = _batchReportService.GetBatchExamReport(batchId);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =====================================================
        // تقرير طالب داخل دفعة
        // =====================================================
        [HttpGet]
        public IActionResult Student(int batchId, int studentId)
        {
            if (batchId <= 0 || studentId <= 0)
                return NotFound();

            var model =
                _studentBatchReportService.GetStudentReport(batchId, studentId);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =====================================================
        // تفاصيل واجب لطالب
        // =====================================================


        [HttpGet]
        public IActionResult HomeworkStudents(int homeworkSetId)
        {
            if (homeworkSetId <= 0)
                return NotFound();

            var model = _studentBatchReportService
                .GetHomeworkStudents(homeworkSetId);

            if (model == null)
                return NotFound();

            return View(model);
        }



        [HttpGet]
        public IActionResult StudentHomeworkDetails(
            int homeworkSetId,
            int studentId)
        {
            if (homeworkSetId <= 0 || studentId <= 0)
                return NotFound();

            var model =
                _studentBatchReportService
                    .GetStudentHomeworkDetails(homeworkSetId, studentId);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =====================================================
        // تفاصيل اختبار لطالب
        // =====================================================
        [HttpGet]
        public IActionResult StudentExamDetails(
            int examAssignmentId,
            int studentId)
        {
            if (examAssignmentId <= 0 || studentId <= 0)
                return NotFound();

            var model =
                _studentBatchReportService
                    .GetStudentExamDetails(examAssignmentId, studentId);

            if (model == null)
                return NotFound();

            return View(model);
        }


        [HttpGet]
        public IActionResult ExamStudents(int examAssignmentId)
        {
            if (examAssignmentId <= 0)
                return NotFound();

            var model = _studentBatchReportService
                .GetExamStudents(examAssignmentId);

            if (model == null)
                return NotFound();

            return View(model);
        }





    }
}
