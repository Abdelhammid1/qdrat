using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    /// صفحة القسم لا تُرجع أي رابط أو معرّف فيديو (C.5)؛ المصدر يُرسل فقط من <see cref="Watch"/> للمحاضرة المفتوحة (OLC-S6).
    /// </summary>
    public class OnlineLearningController : StudentBaseController
    {
        private readonly IOnlineLectureFeatureService _feature;
        private readonly IStudentOnlineLearningService _learning;
        private readonly IStudentLectureWatchService _watch;
        private readonly IStudentLectureProgressService _progress;
        private readonly ILogger<OnlineLearningController> _logger;

        public OnlineLearningController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IOnlineLectureFeatureService feature,
            IStudentOnlineLearningService learning,
            IStudentLectureWatchService watch,
            IStudentLectureProgressService progress,
            ILogger<OnlineLearningController> logger)
            : base(contextFactory, userManager)
        {
            _feature = feature;
            _learning = learning;
            _watch = watch;
            _progress = progress;
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

        // ───────────── OLC-S6: المشغّل المحمي (معيار C.7) ─────────────

        /// <summary>
        /// المفتاح ← R2 (أي شرط ناقص = 404 بلا تمييز) ← R15 (مقفلة ⇒ عودة للقسم برسالة بلا أي بيانات فيديو) ← المشغّل.
        /// المكتملة تُفتح في وضع المراجعة (P11). الصفحة تحمل معرّف الفيديو فلا تُخزَّن في أي كاش.
        /// </summary>
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Watch(int id, CancellationToken ct)
        {
            if (id <= 0 || !await _feature.IsEnabledAsync(ct))
                return NotFound();

            var result = await _watch.GetWatchAsync(StudentId, id, ct);
            switch (result.Outcome)
            {
                case LectureWatchOutcome.Locked:
                    TempData["OlcMessage"] = LockedMessage;
                    return RedirectToAction(nameof(Index));
                case LectureWatchOutcome.Ok when result.Model is not null:
                    return View(result.Model);
                default:
                    return NotFound();
            }
        }

        public const string LockedMessage = "أكمل المحاضرة السابقة أولًا";

        // ───────────── OLC-S7.1: نبضة المشاهدة (JSON — عقد C.5) ─────────────

        /// <summary>
        /// P7/P10: الإتمام وفتح التالي يقررهما الخادم فقط. AntiForgery (ترويسة RequestVerificationToken) + Rate limit
        /// <c>olc-ping</c> (20/دقيقة بالمستخدم لا الـ IP). studentId من سياق الطالب في الخادم — لا يُقبل من الطلب.
        /// 404 = المفتاح مغلق/R2/عنصر لا يتبع المحتوى (بلا تمييز)، 409 = مقفلة (R13/R14)، 400 = طلب غير صالح.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("olc-ping")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Ping([FromBody] StudentLecturePingRequest? request, CancellationToken ct)
        {
            if (request is null)
                return BadRequest(new StudentLecturePingResponse { Ok = false, Reason = "bad-request", Message = "طلب غير صالح." });

            if (!await _feature.IsEnabledAsync(ct))
                return NotFound(new StudentLecturePingResponse { Ok = false, Reason = "notfound", Message = StudentLectureProgressService.NotFoundMessage });

            var result = await _progress.RecordPingAsync(StudentId, request, ct);
            return result.Status switch
            {
                LecturePingStatus.Ok => Ok(result.Body),
                LecturePingStatus.BadRequest => BadRequest(result.Body),
                LecturePingStatus.Conflict => Conflict(result.Body),
                _ => NotFound(result.Body)
            };
        }
    }
}
