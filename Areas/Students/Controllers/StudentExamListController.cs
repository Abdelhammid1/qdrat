using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Students.Exams.Abstractions;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class StudentExamListController : StudentBaseController
    {
        private readonly IStudentExamContextService _examContextService;

        public StudentExamListController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IStudentExamContextService examContextService
        ) : base(contextFactory, userManager)
        {
            _examContextService = examContextService;
        }

        // ===================================================
        // 🟢 جميع الاختبارات
        // ===================================================
        [HttpGet]
        public async Task<IActionResult> All()
        {
            var exams = await _examContextService.GetAllAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            return View("AllExams", exams);
        }

        // ===================================================
        // 🟡 الاختبارات المطلوبة
        // ===================================================
        [HttpGet]
        public async Task<IActionResult> Required()
        {
            var exams = await _examContextService.GetRequiredAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            ViewBag.TotalRequired = exams.Count;
            return View("RequiredExams", exams);
        }

        // ===================================================
        // 🟢 الاختبارات المكتملة
        // ===================================================
        [HttpGet]
        public async Task<IActionResult> Completed()
        {
            var exams = await _examContextService.GetCompletedAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            return View("CompletedExams", exams);
        }

        // ===================================================
        // 🔴 الاختبارات المتأخرة
        // ===================================================
        [HttpGet]
        public async Task<IActionResult> Late()
        {
            var exams = await _examContextService.GetLateAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            return View("LateExams", exams);
        }
    }
}
