using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Remedial;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class RemedialSessionsController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IRemedialTrackingService _trackingService;

        public RemedialSessionsController(IDbContextFactory<ApplicationDbContext> contextFactory, IRemedialTrackingService trackingService)
        {
            _contextFactory = contextFactory;
            _trackingService = trackingService;
        }

        public IActionResult EnterCode()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EnterCode(string code)
        {
            using var _context = _contextFactory.CreateDbContext();
            var session = await _context.RemedialSessions.FirstOrDefaultAsync(s => s.AccessCode == code);
            if (session == null)
            {
                TempData["ErrorMessage"] = "❌ الكود غير صالح.";
                return View();
            }
            return RedirectToAction("Start", new { id = session.Id });
        }

        [HttpPost]
        public async Task<IActionResult> CacheVideoProgress([FromBody] VideoWatchModel model)
        {
            if (model == null || model.StudentId == 0)
                return Json(new { success = false, message = "❌ بيانات غير صالحة" });

            await _trackingService.CacheVideoProgressAsync(HttpContext, model.StudentId, model.SessionId, model.VideoId, model.SecondsWatched);
            return Json(new { success = true, message = "تم تسجيل التقدم مؤقتًا في الجلسة" });
        }

        public async Task<IActionResult> Start(int id)
        {
            using var _context = _contextFactory.CreateDbContext();
            var session = await _context.RemedialSessions
                .Include(s => s.RemedialPlan)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (session == null) return NotFound();
            return View(session);
        }

        public IActionResult Result()
        {
            return View();
        }
    }
}
