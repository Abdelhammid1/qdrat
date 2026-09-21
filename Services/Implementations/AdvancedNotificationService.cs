using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Implementations
{
    public class AdvancedNotificationService : IAdvancedNotificationService
    {
        private readonly ApplicationDbContext _context;

        public AdvancedNotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SendToStudentAsync(int studentId, string message, NotificationCategory category, string? targetUrl = null, int? sentByInstructorId = null)
        {
            var notification = new Notification
            {
                StudentID = studentId,
                Message = message,
                Category = category,
                TargetUrl = targetUrl,
                SentByInstructorId = sentByInstructorId,
                SentAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }
        public async Task SendToInstructorAsync(int instructorId, string message, NotificationCategory category, string? targetUrl = null)
        {
            var notification = new Notification
            {
                SentByInstructorId = instructorId,
                Message = message,
                Category = category,
                TargetUrl = targetUrl,
                SentAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task SendToStudentsAsync(List<int> studentIds, string message, NotificationCategory category, string? targetUrl = null, int? sentByInstructorId = null)
        {
            var notifications = studentIds.Select(id => new Notification
            {
                StudentID = id,
                Message = message,
                Category = category,
                TargetUrl = targetUrl,
                SentByInstructorId = sentByInstructorId,
                SentAt = DateTime.Now
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        public async Task SendToUserAsync(string userId, string message, NotificationCategory category, string? targetUrl = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Message = message,
                Category = category,
                TargetUrl = targetUrl,
                SentAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task SendToRoleAsync(string role, string message, NotificationCategory category, string? targetUrl = null)
        {
            var userIds = await (from ur in _context.UserRoles
                                 join r in _context.Roles on ur.RoleId equals r.Id
                                 where r.Name == role
                                 select ur.UserId).ToListAsync();

            var notifications = userIds.Select(uid => new Notification
            {
                UserId = uid,
                Message = message,
                Category = category,
                TargetUrl = targetUrl,
                SentAt = DateTime.Now
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }
    }
}
