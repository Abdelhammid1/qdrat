// المسار: Data/Seeders/NotificationSeeder.cs

using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Data.Seeders
{
    public static class NotificationSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            if (context.Notifications.Any()) return;

            var studentId = context.Students.Select(s => s.StudentID).FirstOrDefault();
            if (studentId == 0) return;

            var notifications = new List<Notification>
            {
                new Notification
                {
                    StudentID = studentId,
                    Message = "📌 لديك واجب جديد يجب تسليمه خلال يومين.",
                    Category = NotificationCategory.Reminder,
                    SentAt = DateTime.UtcNow.AddHours(-5),
                    IsRead = false
                },
                new Notification
                {
                    StudentID = studentId,
                    Message = "🎉 أحسنت! لقد حصلت على 90% في آخر اختبار.",
                    Category = NotificationCategory.Important,
                    SentAt = DateTime.UtcNow.AddDays(-1),
                    IsRead = false
                },
                new Notification
                {
                    StudentID = studentId,
                    Message = "📢 تم تحديث محتوى الدورة التدريبية الخاصة بك.",
                    Category = NotificationCategory.General,
                    SentAt = DateTime.UtcNow.AddHours(-2),
                    IsRead = true
                }
            };

            context.Notifications.AddRange(notifications);
            context.SaveChanges();
        }
    }
}
