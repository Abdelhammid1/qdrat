using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Homework;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class StudentHomeworkListController : StudentBaseController
    {
        private readonly IStudentHomeworkStatusService _homeworkStatusService;

        public StudentHomeworkListController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IStudentHomeworkStatusService homeworkStatusService
        ) : base(contextFactory, userManager)
        {
            _homeworkStatusService = homeworkStatusService;
        }

        // =====================================================
        // 1️⃣ كل الواجبات
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> AllHomeworks()
        {
            var vm = await _homeworkStatusService.GetAllAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            return View(vm);
        }

        // =====================================================
        // 2️⃣ الواجبات المطلوبة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> RequiredHomeworks()
        {
            var vm = await _homeworkStatusService.GetRequiredAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            var summary = await _homeworkStatusService.GetHomeworkSummaryAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            ViewBag.TotalRequired = summary.Required;
            ViewBag.CurrentAverage = summary.AverageScore;

            return View(vm);
        }

        // =====================================================
        // 3️⃣ الواجبات المتأخرة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> LateHomeworks()
        {
            var vm = await _homeworkStatusService.GetLateAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            ViewBag.LateCount = vm.Count;
            ViewBag.CurrentAverage = 0;

            return View(vm);
        }

        // =====================================================
        // 4️⃣ الواجبات المحلولة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> SolvedHomeworks()
        {
            var vm = await _homeworkStatusService.GetSolvedAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            var summary = await _homeworkStatusService.GetHomeworkSummaryAsync(
                StudentId,
                ActiveCourseId,
                ActiveBatchId
            );

            ViewBag.TotalSolved = summary.Completed;
            ViewBag.AverageScore = summary.AverageScore;

            return View(vm);
        }
    }
}
