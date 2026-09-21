// ===========================
// ✅ Namespace: Areas/Students/Controllers/DashboardController.cs
// ===========================
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Interfaces.Exams;
using QdratNew.Services.StudentAnalysis;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    public class DashboardController : StudentBaseController
    {
        private readonly IStudentDashboardService _dashboardService;
        private readonly IStudentRankingService _studentRankingService;
        private readonly IStudentPerformanceAnalysisService _service;
        private readonly IStudentCourseDashboardService _courseDashboardService;

        public DashboardController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IStudentDashboardService dashboardService,
            IStudentRankingService studentRankingService,
            IStudentPerformanceAnalysisService service,
            IStudentCourseDashboardService courseDashboardService
        ) : base(contextFactory, userManager)
        {
            _dashboardService = dashboardService;
            _studentRankingService = studentRankingService;
            _service = service;
            _courseDashboardService = courseDashboardService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            int studentId = StudentId;
            int courseId = ActiveCourseId;
            int batchId = ActiveBatchId;

            var vm = await _courseDashboardService
                .GetCourseDashboardAsync(studentId, courseId, batchId);

            // =====================================================
            // 🔹 ترتيب الطالب (غير مرتبط بدورة)
            // =====================================================
            var rankVm = await _studentRankingService
                .GetCurrentRankAsync(studentId);

            if (rankVm != null)
            {
                vm.StudentRank = new StudentRankViewModel
                {
                    Rank = rankVm.Rank,
                    TotalStudents = rankVm.TotalStudents,
                    MedalImageUrl = rankVm.MedalImageUrl,
                    MotivationalMessage = rankVm.MotivationalMessage,
                    LastUpdated = rankVm.LastUpdated
                };
            }

            return View(vm);
        }



        // =====================================================
        // 🔹 AJAX: تحديث بيانات الداشبورد عند تغيير الدفعة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> DashboardData(int batchId)
        {
            int studentId = StudentId;

            using var db = _contextFactory.CreateDbContext();

            // جلب CourseId المرتبط بالدفعة
            var courseId = await db.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.CourseId)
                .FirstOrDefaultAsync();

            if (courseId <= 0)
                return BadRequest();

            var vm = await _courseDashboardService
                .GetCourseDashboardAsync(studentId, courseId, batchId);

            return Json(new
            {
                homeworkProgress = vm.HomeworkProgress,
                examProgress = vm.ExamProgress,
                homeworkComparison = vm.HomeworkComparison,
                examComparison = vm.ExamComparison
            });
        }
        public async Task<IActionResult> Analysis()
        {
            int studentId = StudentId;

            var report = await _service.BuildStudentReportAsync(studentId);

            return View(report);
        }
    }
}