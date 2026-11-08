using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.OnlineLecture;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Areas.Students.Controllers
{
    /// <summary>
    /// OLC-S5 — قسم «المحتوى التعليمي» للطالب (R12) على سياق الكورس النشط في <see cref="StudentBaseController"/>.
    /// كل Action يفحص المفتاح والوصول على الخادم (R3) — إخفاء الرابط في القائمة ليس حماية.
    /// الصفحة لا تُرجع أي رابط أو معرّف فيديو (C.5)؛ المشاهدة في OLC-S6.
    /// </summary>
    public class OnlineLearningController : StudentBaseController
    {
        private readonly IOnlineLectureFeatureService _feature;
        private readonly IStudentOnlineLearningService _learning;
        private readonly ILogger<OnlineLearningController> _logger;

        public OnlineLearningController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IOnlineLectureFeatureService feature,
            IStudentOnlineLearningService learning,
            ILogger<OnlineLearningController> logger)
            : base(contextFactory, userManager)
        {
            _feature = feature;
            _learning = learning;
            _logger = logger;
        }

        // ───────────── OLC-S5.3: المناهج ثم المحاضرات بحالة مكتملة/مفتوحة/مقفلة ─────────────

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            // R11: الميزة المغلقة = 404 بلا تمييز
            if (!await _feature.IsEnabledAsync(ct))
                return NotFound();

            StudentOnlineLearningIndexVm vm;
            try
            {
                vm = await _learning.GetIndexAsync(StudentId, ActiveCourseId, ActiveBatchId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "OLC: failed to load learning content for student {StudentId}, course {CourseId}", StudentId, ActiveCourseId);
                vm = new StudentOnlineLearningIndexVm { IsEligible = true, LoadFailed = true };
            }

            vm.CourseTitle = string.IsNullOrWhiteSpace(vm.CourseTitle) ? CourseContext?.CourseTitle ?? string.Empty : vm.CourseTitle;
            vm.BatchTitle = string.IsNullOrWhiteSpace(vm.BatchTitle) ? CourseContext?.BatchTitle ?? string.Empty : vm.BatchTitle;
            return View(vm);
        }
    }
}
