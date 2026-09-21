using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class StudentExamPagesController : StudentBaseController
    {
        private readonly IStudentExamStatusService _examStatusService;

        public StudentExamPagesController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IStudentExamStatusService examStatusService
        ) : base(contextFactory, userManager)
        {
            _examStatusService = examStatusService;
        }

        // =====================================================
        // 📌 جميع الاختبارات (حسب الدورة + الدفعة المختارة)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> AllExams()
        {
            var data = await _examStatusService.GetAllExamsAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            return View(data);
        }

        // =====================================================
        // 📌 الاختبارات المطلوبة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> RequiredExams()
        {
            var vm = await _examStatusService.GetRequiredExamsAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            ViewBag.TotalRequired = vm.Count;
            return View(vm);
        }

        // =====================================================
        // 📌 الاختبارات المحلولة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> CompletedExams()
        {
            var vm = await _examStatusService.GetSolvedExamsAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            return View(vm);
        }

        // =====================================================
        // 📌 الاختبارات المتأخرة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> LateExams()
        {
            var late = await _examStatusService.GetLateExamsPageAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            var vm = new LateExamsPageVm
            {
                Late = late,
                ExpiringSoon = new(),
                Upcoming = new(),
                TotalLate = late.Count,
                TotalExpiringSoon = 0,
                TotalUpcoming = 0
            };

            return View(vm);
        }

    }
}
