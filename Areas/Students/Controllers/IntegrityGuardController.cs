using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.IntegrityGuard;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class IntegrityGuardController : StudentBaseController
    {
        private readonly IIntegrityGuardService _integrityGuard;

        public IntegrityGuardController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IIntegrityGuardService integrityGuard
        ) : base(contextFactory, userManager)
        {
            _integrityGuard = integrityGuard;
        }

        public class ReportViolationRequest
        {
            public IntegrityAttemptType AttemptType { get; set; }
            public int AttemptEntityId { get; set; }
            public string ViolationType { get; set; } = "BrowserTranslate";
            public string? PageUrl { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportViolation([FromBody] ReportViolationRequest request)
        {
            if (request == null) return BadRequest();

            var studentId = StudentId;
            if (studentId <= 0) return Unauthorized();

            await _integrityGuard.LogViolationAsync(
                studentId,
                request.AttemptType,
                request.AttemptEntityId,
                request.ViolationType,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers["User-Agent"].ToString(),
                request.PageUrl);

            var canSelfResolve = await _integrityGuard.CanSelfResolveAsync(studentId, request.AttemptType, request.AttemptEntityId);

            return Json(new { success = true, canSelfResolve });
        }

        public class SelfResolveRequest
        {
            public IntegrityAttemptType AttemptType { get; set; }
            public int AttemptEntityId { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelfResolve([FromBody] SelfResolveRequest request)
        {
            if (request == null) return BadRequest();

            var studentId = StudentId;
            if (studentId <= 0) return Unauthorized();

            var (success, message) = await _integrityGuard.TrySelfResolveAsync(studentId, request.AttemptType, request.AttemptEntityId);

            return Json(new { success, message });
        }

        [HttpGet]
        public async Task<IActionResult> Blocked(int? attemptType, int? attemptEntityId, string? returnUrl)
        {
            var vm = new BlockedViewModel
            {
                AttemptType = attemptType,
                AttemptEntityId = attemptEntityId,
                ReturnUrl = (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) ? returnUrl : null
            };

            if (attemptType.HasValue && attemptEntityId.HasValue && StudentId > 0)
            {
                vm.CanSelfResolve = await _integrityGuard.CanSelfResolveAsync(
                    StudentId, (IntegrityAttemptType)attemptType.Value, attemptEntityId.Value);
            }

            return View(vm);
        }
    }
}
