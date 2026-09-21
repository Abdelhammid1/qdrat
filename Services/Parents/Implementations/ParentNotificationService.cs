using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Parents.Interfaces;
using System;
using System.Threading.Tasks;

namespace QdratNew.Services.Parents.Implementations
{
    public class ParentNotificationService : IParentNotificationService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ParentNotificationService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task SendParentNotificationAsync(int parentId, int studentId, string message, string? targetUrl = null)
        {
            using var db = _contextFactory.CreateDbContext();

            var userId = await db.Parents
                .AsNoTracking()
                .Where(p => p.ParentID == parentId)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(userId)) return;

            var notification = new Notification
            {
                UserId = userId,
                ParentID = parentId,
                StudentID = studentId,
                Message = message,
                TargetUrl = targetUrl,
                IsRead = false,
                SentAt = DateTime.Now,
                Category = NotificationCategory.General
            };

            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
        }
    }
}
