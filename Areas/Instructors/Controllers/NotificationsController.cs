using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Notifications;

namespace QdratNew.Areas.Instructors.Controllers
{
    public class NotificationsController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _context = context;
        }

        public async Task<IActionResult> Sent()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Forbid();

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.SentByInstructorId == instructorId)
                .OrderByDescending(n => n.SentAt)
                .Select(n => new NotificationViewModel
                {
                    StudentName = n.Student != null ? n.Student.FullName : null,
                    Message = n.Message,
                    Category = n.Category.ToString(),
                    SentAt = n.SentAt,
                    TargetUrl = n.TargetUrl
                })
                .ToListAsync();

            return View(notifications);
        }
    }
}
