using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Areas.Instructors.Controllers.Exam
{
    [Area("Instructors")]
    public class InstructorExamMonitoringController : BaseInstructorController
    {
        private readonly IInstructorExamMonitoringService _monitoringService;

        public InstructorExamMonitoringController(
            IInstructorExamMonitoringService monitoringService,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _monitoringService = monitoringService;
        }

        // =====================================================
        // 👨‍🎓 طلاب الاختبار
        // =====================================================
        public async Task<IActionResult> Students(int examAssignmentId, string type)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            PartnerExamStudentsVM vm;

            if (type == "Individual")
            {
                vm = await _monitoringService.GetIndividualExamStudentsAsync(examAssignmentId, instructorId);
            }
            else
            {
                vm = await _monitoringService.GetExamStudentsAsync(examAssignmentId, instructorId);
            }

            if (vm == null)
                return NotFound();

            return View(vm);
        }
        // =====================================================
        // 📈 محاولات الطالب
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Attempts(int examAssignmentId, int studentId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var vm = await _monitoringService.GetStudentAttemptsAsync(
                examAssignmentId,
                studentId,
                instructorId);

            if (vm == null)
                return NotFound();

            return View(vm);
        }



        [HttpGet]
        public async Task<IActionResult> ReviewExamStudent(int studentId, int examAssignmentId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var vm = await _monitoringService.GetExamStudentReviewAsync(
                examAssignmentId,
                studentId,
                instructorId);

            if (vm == null)
                return NotFound();

            return View("ReviewExamStudent", vm);
        }



        [HttpGet]
        public async Task<IActionResult> ExamStudentReport(int examAssignmentId, int studentId, bool print = false)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var vm = await _monitoringService.GetExamStudentReportAsync(
                examAssignmentId,
                studentId,
                instructorId);

            if (vm == null)
                return NotFound();

            if (print)
                return View("ExamStudentReport_Print", vm);

            return View("ExamStudentReport", vm);
        }



        // =====================================================
        // 📊 تقرير الاختبار (جديد)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Report(int examAssignmentId)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var vm = await _monitoringService.GetExamReportAsync(
                examAssignmentId,
                instructorId);

            if (vm == null)
                return NotFound();

            return View(vm);
        }
    }
}