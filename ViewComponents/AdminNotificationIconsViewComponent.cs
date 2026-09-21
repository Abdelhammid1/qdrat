using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Layout;

namespace QdratNew.ViewComponents
{
    public class AdminNotificationIconsViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public AdminNotificationIconsViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var viewModel = new AdminNotificationIconsViewModel
            {
                NotificationsCount = await _context.Notifications
                    .CountAsync(n => !n.IsRead && n.SentAt.Date == DateTime.Today),

                TasksCount = await _context.Lecture
                    .CountAsync(l => l.Date == DateTime.Today)
            };

            return View("_NotificationIcons", viewModel);
        }
    }
}
