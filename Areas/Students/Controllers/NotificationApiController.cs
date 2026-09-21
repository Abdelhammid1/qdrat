using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Notifications;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    [Route("Students/Notifications")]
    public class NotificationApiController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("Widget")]
        public async Task<IActionResult> GetWidgetData()
        {
            var userId = User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var studentId = await _context.Students
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .Select(s => (int?)s.StudentID)
                .FirstOrDefaultAsync();

            if (studentId == null)
                return NotFound();

            var unreadCount = await _context.Notifications
                .AsNoTracking()
                .CountAsync(n => n.StudentID == studentId.Value && !n.IsRead);

            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);

            var todayTasksCount = await _context.Homeworks
                .AsNoTracking()
                .CountAsync(h =>
                    h.StudentId == studentId.Value &&
                    h.AssignedAt >= todayStart &&
                    h.AssignedAt < todayEnd &&
                    h.Status != Enums.HomeworkStatus.Submitted);

            return Json(new NotificationWidgetViewModel
            {
                UnreadCount = unreadCount,
                TodayTasksCount = todayTasksCount
            });
        }
    }
}
