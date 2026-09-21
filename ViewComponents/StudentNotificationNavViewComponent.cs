using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.ViewModels.Layout;

namespace QdratNew.ViewComponents
{
    public class StudentNotificationNavViewComponent : ViewComponent
    {
        private const string StudentIndividualExamsUrlPart = "/Students/StudentIndividualExams";
        private const string StartIndividualExamUrlPart = "StartIndividualExam";
        private readonly ApplicationDbContext _context;

        public StudentNotificationNavViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(string display = "Bell")
        {
            var model = await BuildModelAsync();
            var viewName = string.Equals(display, "Sidebar", StringComparison.OrdinalIgnoreCase)
                ? "Sidebar"
                : "Bell";

            return View(viewName, model);
        }

        private async Task<StudentNotificationNavViewModel> BuildModelAsync()
        {
            var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new StudentNotificationNavViewModel();
            }

            var studentId = await _context.Students
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .Select(s => (int?)s.StudentID)
                .FirstOrDefaultAsync();

            if (studentId == null)
            {
                return new StudentNotificationNavViewModel();
            }

            var notifications = _context.Notifications
                .AsNoTracking()
                .Where(n => n.StudentID == studentId.Value);

            var unreadNotifications = notifications.Where(n => !n.IsRead);

            var totalUnreadCount = await unreadNotifications.CountAsync();
            var homeworkUnreadCount = await unreadNotifications.CountAsync(n =>
                n.Category == NotificationCategory.Homework ||
                (n.Category == NotificationCategory.Reminder && n.Message.Contains("واجب")) ||
                (n.TargetUrl != null && n.TargetUrl.Contains("Homework")));

            var individualExamUnreadCount = await unreadNotifications.CountAsync(n =>
                n.Category == NotificationCategory.Exam &&
                ((n.TargetUrl != null &&
                    (n.TargetUrl.Contains(StudentIndividualExamsUrlPart) ||
                     n.TargetUrl.Contains(StartIndividualExamUrlPart))) ||
                 n.Message.Contains("فرد")));

            var examUnreadCount = await unreadNotifications.CountAsync(n =>
                n.Category == NotificationCategory.Exam &&
                (n.TargetUrl == null ||
                 (!n.TargetUrl.Contains(StudentIndividualExamsUrlPart) &&
                  !n.TargetUrl.Contains(StartIndividualExamUrlPart))) &&
                !n.Message.Contains("فرد"));

            var recentNotifications = await notifications
                .OrderByDescending(n => n.SentAt)
                .Select(n => new StudentNotificationNavItemViewModel
                {
                    Message = n.Message,
                    SentAt = n.SentAt,
                    IsRead = n.IsRead,
                    Category = n.Category,
                    TargetUrl = n.TargetUrl
                })
                .Take(5)
                .ToListAsync();

            return new StudentNotificationNavViewModel
            {
                TotalUnreadCount = totalUnreadCount,
                HomeworkUnreadCount = homeworkUnreadCount,
                ExamUnreadCount = examUnreadCount,
                IndividualExamUnreadCount = individualExamUnreadCount,
                RecentNotifications = recentNotifications
            };
        }
    }
}
