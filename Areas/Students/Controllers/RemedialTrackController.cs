using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Areas.Students.Controllers
{
    /// <summary>
    /// RTK-S4: منطقة الطالب في «الخطة العلاجية» (القائمة، بوابة الرقم المرجعي، المحاور، تقدّم الفيديو).
    /// كل Action يمرّ على بوابة الوصول في الخادم (D14) — إخفاء الزر ليس صلاحية.
    /// </summary>
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class RemedialTrackController : Controller
    {
        private readonly IStudentIdentityService _identity;
        private readonly IRemedialTrackAccessService _access;
        private readonly IRemedialTrackProgressService _progress;
        private readonly ITimeZoneService _tz;

        public RemedialTrackController(
            IStudentIdentityService identity,
            IRemedialTrackAccessService access,
            IRemedialTrackProgressService progress,
            ITimeZoneService tz)
        {
            _identity = identity;
            _access = access;
            _progress = progress;
            _tz = tz;
        }

        // ───────────── RTK-S4.1: خطتي العلاجية ─────────────

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var studentId = await _identity.GetCurrentStudentIdAsync(User);
            if (studentId == 0) return Challenge();

            var vm = await _progress.GetMyPlansAsync(studentId, ct);
            return View(vm);
        }

        // ───────────── RTK-S4.3: صفحة الخطة (بعد البوابة) ─────────────

        [HttpGet]
        public async Task<IActionResult> Open(int enrollmentId, CancellationToken ct)
        {
            var studentId = await _identity.GetCurrentStudentIdAsync(User);
            if (studentId == 0) return Challenge();

            var gate = await _access.EvaluateAsync(studentId, enrollmentId, ct);
            var blocked = HandleGate(gate, enrollmentId);
            if (blocked is not null) return blocked;

            var vm = await _progress.GetPlanAsync(studentId, enrollmentId, ct);
            if (vm is null) return NotFound();
            return View(vm);
        }

        // ───────────── RTK-S4.2: التحقق من الرقم المرجعي ─────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("rtk-code")]
        public async Task<IActionResult> VerifyCode(StudentRemedialTrackVerifyCodeInput input, CancellationToken ct)
        {
            var studentId = await _identity.GetCurrentStudentIdAsync(User);
            if (studentId == 0) return Challenge();

            var result = await _access.VerifyCodeAsync(studentId, input.EnrollmentId, input.Code, ct);
            switch (result.Outcome)
            {
                case RemedialTrackVerifyOutcome.Verified:
                    return RedirectToAction(nameof(Open), new { enrollmentId = input.EnrollmentId });

                case RemedialTrackVerifyOutcome.NotFound:
                case RemedialTrackVerifyOutcome.NotYetPublished:
                    return NotFound();

                case RemedialTrackVerifyOutcome.Cancelled:
                    return View("Unavailable");

                case RemedialTrackVerifyOutcome.Locked:
                    return View("CodeGate", await BuildGateAsync(studentId, input.EnrollmentId, result.LockedUntilUtc, null, ct));

                default: // Invalid — رسالة عامة دائمًا
                    return View("CodeGate", await BuildGateAsync(studentId, input.EnrollmentId, null,
                        "الرقم غير صحيح. تأكد من الرقم المرجعي الذي أعطاه لك المعهد.", ct));
            }
        }

        // ───────────── RTK-S4.3: صفحة المحور ─────────────

        [HttpGet]
        public async Task<IActionResult> Axis(int enrollmentId, int axisProgressId, CancellationToken ct)
        {
            var studentId = await _identity.GetCurrentStudentIdAsync(User);
            if (studentId == 0) return Challenge();

            var gate = await _access.EvaluateAsync(studentId, enrollmentId, ct);
            var blocked = HandleGate(gate, enrollmentId);
            if (blocked is not null) return blocked;

            var vm = await _progress.GetAxisAsync(studentId, enrollmentId, axisProgressId, ct);
            if (vm is null) return NotFound();
            return View(vm);
        }

        // ───────────── RTK-S4.4: نبضة الفيديو (JSON) ─────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("rtk-ping")]
        public async Task<IActionResult> VideoPing([FromBody] RemedialTrackVideoPingRequest request, CancellationToken ct)
        {
            if (request is null) return BadRequest(new RemedialTrackVideoPingResponse { Ok = false, Reason = "bad-request" });

            var studentId = await _identity.GetCurrentStudentIdAsync(User);
            if (studentId == 0) return Unauthorized();

            var result = await _progress.RecordPingAsync(studentId, request, ct);
            return result.Status switch
            {
                RemedialTrackPingStatus.Ok => Ok(result.Body),
                RemedialTrackPingStatus.BadRequest => BadRequest(result.Body),
                RemedialTrackPingStatus.NotFound => NotFound(result.Body),
                RemedialTrackPingStatus.Conflict => Conflict(result.Body),
                _ => StatusCode(StatusCodes.Status403Forbidden, result.Body)
            };
        }

        // ───────────── مساعدات ─────────────

        // يحوّل نتيجة البوابة إلى استجابة؛ null = مسموح
        private IActionResult? HandleGate(RemedialTrackAccessResult gate, int enrollmentId)
        {
            switch (gate.Outcome)
            {
                case RemedialTrackAccessOutcome.Allowed:
                    return null;
                case RemedialTrackAccessOutcome.NotFound:
                case RemedialTrackAccessOutcome.NotYetPublished:
                    return NotFound();
                case RemedialTrackAccessOutcome.Cancelled:
                    return View("Unavailable");
                default: // NeedsCode | CodeLocked
                    return View("CodeGate", new StudentRemedialTrackCodeGateVm
                    {
                        EnrollmentId = enrollmentId,
                        TrackTitle = gate.TrackTitle ?? string.Empty,
                        LockedUntilLocal = gate.LockedUntilUtc.HasValue ? _tz.ConvertToSaudi(gate.LockedUntilUtc.Value) : null
                    });
            }
        }

        private async Task<StudentRemedialTrackCodeGateVm> BuildGateAsync(
            int studentId, int enrollmentId, DateTime? lockedUntilUtc, string? error, CancellationToken ct)
        {
            var gate = await _access.EvaluateAsync(studentId, enrollmentId, ct);
            return new StudentRemedialTrackCodeGateVm
            {
                EnrollmentId = enrollmentId,
                TrackTitle = gate.TrackTitle ?? string.Empty,
                LockedUntilLocal = lockedUntilUtc.HasValue ? _tz.ConvertToSaudi(lockedUntilUtc.Value) : null,
                ErrorMessage = error
            };
        }
    }
}
