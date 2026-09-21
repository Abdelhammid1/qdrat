using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Notifications;

namespace QdratNew.Jobs
{
    public class HomeworkReminderJob
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationCenterService _notificationService;

        public HomeworkReminderJob(ApplicationDbContext context, INotificationCenterService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task RunAsync()
        {
            var deadline = DateTime.UtcNow.AddHours(-48);

            var overdueHomeworks = await _context.Homeworks
                .Include(h => h.Lesson)
                .Where(h => h.IsSent && !h.IsCompleted && h.AssignedAt <= deadline)
                .GroupBy(h => h.StudentId)
                .Select(g => new
                {
                    StudentId = g.Key,
                    Count = g.Count(),
                    Lessons = g.Select(x => x.Lesson.Title).Distinct().ToList()
                })
                .ToListAsync();

            foreach (var item in overdueHomeworks)
            {
                string lessonList = string.Join("، ", item.Lessons.Take(3));
                string message = $"⏰ لم تقم بحل {item.Count} واجب حتى الآن، منها: {lessonList}";

                await _notificationService.SendAsync(new Notification
                {
                    StudentID = item.StudentId,
                    Message = message,
                    Category = NotificationCategory.Reminder
                });
            }
        }
    }
}
