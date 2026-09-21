using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Services.Notifications
{
    public interface INotificationCenterService
    {
        Task SendToStudentAsync(int studentId, string message, NotificationCategory category = NotificationCategory.General);
        Task SendToBatchAsync(int batchId, string message, NotificationCategory category = NotificationCategory.General);
        Task SendToCourseAsync(int courseId, string message, NotificationCategory category = NotificationCategory.General);
        Task SendToAllAsync(string message, NotificationCategory category = NotificationCategory.General);
        Task SendAsync(Notification notification);
    }

    public class NotificationCenterService : INotificationCenterService
    {
        private readonly ApplicationDbContext _context;

        public NotificationCenterService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SendToStudentAsync(int studentId, string message, NotificationCategory category = NotificationCategory.General)
        {
            var notification = new Notification
            {
                StudentID = studentId,
                Message = message,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                Category = category
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task SendToBatchAsync(int batchId, string message, NotificationCategory category = NotificationCategory.General)
        {
            var studentIds = await _context.StudentBatchEnrollments
                .Where(s => s.BatchId == batchId)
                .Select(s => s.StudentID)
                .ToListAsync();

            var notifications = studentIds.Select(studentId => new Notification
            {
                StudentID = studentId,
                Message = message,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                Category = category
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        public async Task SendToCourseAsync(int courseId, string message, NotificationCategory category = NotificationCategory.General)
        {
            var studentIds = await _context.StudentCourses
                .Where(sc => sc.CourseId == courseId)
                .Select(sc => sc.StudentID)
                .ToListAsync();

            var notifications = studentIds.Select(studentId => new Notification
            {
                StudentID = studentId,
                Message = message,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                Category = category
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        public async Task SendToAllAsync(string message, NotificationCategory category = NotificationCategory.General)
        {
            var studentIds = await _context.Students
                .Select(s => s.StudentID)
                .ToListAsync();

            var notifications = studentIds.Select(studentId => new Notification
            {
                StudentID = studentId,
                Message = message,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                Category = category
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        public async Task SendAsync(Notification notification)
        {
            if (notification == null || string.IsNullOrWhiteSpace(notification.Message))
                return;

            notification.SentAt = DateTime.UtcNow;
            notification.IsRead = false;

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }
    }
}
