using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Unauthorized();

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.StudentID == studentId.Value)
                .OrderByDescending(n => n.SentAt)
                .Take(50)
                .Select(n => new StudentNotificationViewModel
                {
                    NotificationId = n.NotificationId,
                    Message = n.Message,
                    SentAt = n.SentAt,
                    IsRead = n.IsRead,
                    Category = n.Category,
                    TargetUrl = n.TargetUrl
                })
                .ToListAsync();

            return View(notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Unauthorized();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.StudentID == studentId.Value);

            if (notification == null)
                return NotFound();

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Unauthorized();

            var unreadNotifications = await _context.Notifications
                .Where(n => n.StudentID == studentId.Value && !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Count > 0)
            {
                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                }

                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, updatedCount = unreadNotifications.Count });
        }

        private async Task<int?> GetCurrentStudentIdAsync()
        {
            var userId = User.FindFirst("sub")?.Value
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (string.IsNullOrEmpty(userId))
                return null;

            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            return student?.StudentID;
        }
    }
}
