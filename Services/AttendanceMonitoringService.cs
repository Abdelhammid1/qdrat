using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Notifications;

namespace QdratNew.Services
{
    public class AttendanceMonitoringService : IAttendanceMonitoringService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationCenterService _notificationService;

        public AttendanceMonitoringService(ApplicationDbContext context, INotificationCenterService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task CheckMissingBatchLessonCompletionsAsync()
        {
            var today = DateTime.Today;

            var todayLectures = await _context.Lecture
                .Include(l => l.Batch)
                .Where(l => l.Date.Date == today)
                .ToListAsync();

            foreach (var lecture in todayLectures)
            {
                var batchId = lecture.BatchId;

                bool hasCompletedLessons = await _context.BatchLessonCompletions
                    .AnyAsync(b => b.BatchId == batchId);

                if (!hasCompletedLessons)
                {
                    var adminRoleIds = await _context.Roles
                        .Where(r => r.Name == "Admin" || r.Name == "SuperAdmin")
                        .Select(r => r.Id)
                        .ToListAsync();

                    var adminUserIds = await _context.UserRoles
                        .Where(ur => adminRoleIds.Contains(ur.RoleId))
                        .Select(ur => ur.UserId)
                        .ToListAsync();

                    var admins = await _context.Users
                        .Where(u => adminUserIds.Contains(u.Id))
                        .ToListAsync();

                    foreach (var admin in admins)
                    {
                        await _notificationService.SendAsync(new Entities.Notification
                        {
                            UserId = admin.Id,
                            Message = $"⚠️ الدفعة ({lecture.Batch.Name}) لديها محاضرة اليوم ({lecture.Date:yyyy/MM/dd}) ولم يسجل المدرب المؤشرات الخاصة بها حتى الآن.",
                            SentAt = DateTime.Now,
                            Category = NotificationCategory.Important
                        });
                    }
                }
            }
        }

    }
}
