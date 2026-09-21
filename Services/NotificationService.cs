using System;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task SendNotificationToAdminAsync(string message, string category)
{
    var adminRoleId = await _context.Roles
        .AsNoTracking()
        .Where(r => r.Name == "Admin")
        .Select(r => r.Id)
        .FirstOrDefaultAsync();

    if (adminRoleId == null) return;

    var adminUserIds = await _context.UserRoles
        .AsNoTracking()
        .Where(ur => ur.RoleId == adminRoleId)
        .Select(ur => ur.UserId)
        .ToListAsync();

    if (!adminUserIds.Any()) return;

    var notifications = adminUserIds.Select(userId => new Notification
    {
        UserId = userId,
        Message = message,
        Category = Enum.Parse<NotificationCategory>(category),
        SentAt = DateTime.Now,
        IsRead = false
    }).ToList();

    await _context.Notifications.AddRangeAsync(notifications);
    await _context.SaveChangesAsync();
}

        // 🔹 إرسال إشعار جديد
        public void SendNotification(int studentId, string message, string category)
        {
            var notification = new Notification
            {
                StudentID = studentId,
                Message = message,
                Category = Enum.Parse<NotificationCategory>(category)
            };

            _context.Notifications.Add(notification);
            _context.SaveChanges();
        }
    }
}
