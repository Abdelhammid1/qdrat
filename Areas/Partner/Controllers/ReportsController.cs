using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QdratNew.Attributes;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Reports.Interfaces;
using System.Security.Claims;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    [Authorize(Roles = "Partner")]
    public class ReportsController : Controller
    {
        private readonly IBatchReportService _batchReportService;
        private readonly IStudentBatchReportService _studentBatchReportService;
        private readonly ApplicationDbContext _context;

        private int ActivePartnerId =>
            HttpContext.Session.GetInt32("ActivePartnerId") ?? 0;

        public ReportsController(
            IBatchReportService batchReportService,
            IStudentBatchReportService studentBatchReportService,
            ApplicationDbContext context)
        {
            _batchReportService = batchReportService;
            _studentBatchReportService = studentBatchReportService;
            _context = context;
        }

        // PPL-S1: لا يرث PartnerBaseController حتى تبقى التقارير متاحة بعد انتهاء العقد،
        // لذلك يتحقق هنا أن الشريك النشط يخص المستخدم الحالي
        public override async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var partnerId = ActivePartnerId;

            var linked = partnerId > 0 && await _context.UserPartners
                .AnyAsync(up => up.UserId == userId && up.PartnerId == partnerId);

            if (!linked)
            {
                HttpContext.Session.Remove("ActivePartnerId");
                context.Result = RedirectToAction("Select", "PartnerContext");
                return;
            }

            await base.OnActionExecutionAsync(context, next);
        }

        // =====================================================
        // تقرير الدفعة (ملخص عام)
        // =====================================================
        [HttpGet]
        public IActionResult Batch(int batchId)
        {
            if (batchId <= 0)
                return NotFound();

            var model = _batchReportService.GetBatchReport(ActivePartnerId, batchId);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =====================================================
        // تقرير الواجبات داخل الدفعة
        // =====================================================
        [HttpGet]
        [PartnerFeature(PartnerFeatureType.Homework, AllowReadAfterExpiry = true)] // PPL-S1.4
        public IActionResult BatchHomeworks(int batchId)
        {
            if (batchId <= 0)
                return NotFound();

            var model = _batchReportService.GetBatchHomeworkReport(ActivePartnerId, batchId);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =====================================================
        // تقرير الاختبارات داخل الدفعة
        // =====================================================
        [HttpGet]
        [PartnerFeature(PartnerFeatureType.Exams, AllowReadAfterExpiry = true)] // PPL-S1.4
        public IActionResult BatchExams(int batchId)
        {
            if (batchId <= 0)
                return NotFound();

            var model = _batchReportService.GetBatchExamReport(ActivePartnerId, batchId);

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
                _studentBatchReportService.GetStudentReport(ActivePartnerId, batchId, studentId);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =====================================================
        // تفاصيل واجب لطالب
        // =====================================================


        [HttpGet]
        [PartnerFeature(PartnerFeatureType.Homework, AllowReadAfterExpiry = true)] // PPL-S1.4
        public IActionResult HomeworkStudents(int homeworkSetId)
        {
            if (homeworkSetId <= 0)
                return NotFound();

            var model = _studentBatchReportService
                .GetHomeworkStudents(ActivePartnerId, homeworkSetId);

            if (model == null)
                return NotFound();

            return View(model);
        }



        [HttpGet]
        [PartnerFeature(PartnerFeatureType.Homework, AllowReadAfterExpiry = true)] // PPL-S1.4
        public IActionResult StudentHomeworkDetails(
            int homeworkSetId,
            int studentId)
        {
            if (homeworkSetId <= 0 || studentId <= 0)
                return NotFound();

            var model =
                _studentBatchReportService
                    .GetStudentHomeworkDetails(ActivePartnerId, homeworkSetId, studentId);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =====================================================
        // تفاصيل اختبار لطالب
        // =====================================================
        [HttpGet]
        [PartnerFeature(PartnerFeatureType.Exams, AllowReadAfterExpiry = true)] // PPL-S1.4
        public IActionResult StudentExamDetails(
            int examAssignmentId,
            int studentId)
        {
            if (examAssignmentId <= 0 || studentId <= 0)
                return NotFound();

            var model =
                _studentBatchReportService
                    .GetStudentExamDetails(ActivePartnerId, examAssignmentId, studentId);

            if (model == null)
                return NotFound();

            return View(model);
        }


        [HttpGet]
        [PartnerFeature(PartnerFeatureType.Exams, AllowReadAfterExpiry = true)] // PPL-S1.4
        public IActionResult ExamStudents(int examAssignmentId)
        {
            if (examAssignmentId <= 0)
                return NotFound();

            var model = _studentBatchReportService
                .GetExamStudents(ActivePartnerId, examAssignmentId);

            if (model == null)
                return NotFound();

            return View(model);
        }





    }
}
