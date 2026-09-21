using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Areas.Parents.Controllers.Base;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.ViewModels.Parents;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Parents.Controllers
{
    public class NotificationsController : ParentBaseController
    {
        public NotificationsController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IParentAccessService parentAccessService)
            : base(contextFactory, userManager, parentAccessService)
        { }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;
            using var db = _contextFactory.CreateDbContext();

            var dbNotifs = await db.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.SentAt)
                .Take(50)
                .ToListAsync();

            var vm = new ParentNotificationViewModel
            {
                Notifications = dbNotifs.Select(n => new ParentNotificationItemViewModel
                {
                    Id = n.NotificationId,
                    Title = "إشعار",
                    Message = n.Message,
                    TargetUrl = n.TargetUrl,
                    IsRead = n.IsRead,
                    TimeAgo = GetTimeAgo(n.SentAt),
                    IconClass = GetIcon(n.Category),
                    BadgeColor = n.IsRead ? "secondary" : "primary"
                }).ToList(),
                UnreadCount = dbNotifs.Count(n => !n.IsRead)
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            using var db = _contextFactory.CreateDbContext();

            var notif = await db.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notif != null)
            {
                notif.IsRead = true;
                await db.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        private static string GetTimeAgo(System.DateTime dt)
        {
            var diff = System.DateTime.Now - dt;
            if (diff.TotalMinutes < 60) return $"منذ {(int)diff.TotalMinutes} دقيقة";
            if (diff.TotalHours < 24) return $"منذ {(int)diff.TotalHours} ساعة";
            return $"منذ {(int)diff.TotalDays} يوم";
        }

        private static string GetIcon(QdratNew.Enums.NotificationCategory cat) => cat switch
        {
            QdratNew.Enums.NotificationCategory.Homework => "fa-book-open",
            QdratNew.Enums.NotificationCategory.Exam => "fa-file-alt",
            QdratNew.Enums.NotificationCategory.Remedial => "fa-user-graduate",
            _ => "fa-bell"
        };
    }
}
