using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Analytics;
using QdratNew.Services.Interfaces;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("Admin/Analytics/[action]")]
    [Authorize(Roles = "Owner,Developer")]
    public class AnalyticsBulkActionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAdvancedNotificationService _notificationService;
        private readonly IAnalyticsDashboardService _dashboardService;

        public AnalyticsBulkActionsController(
            ApplicationDbContext context,
            IAdvancedNotificationService notificationService,
            IAnalyticsDashboardService dashboardService)
        {
            _context = context;
            _notificationService = notificationService;
            _dashboardService = dashboardService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBulkNotification(
            [FromForm] List<int> studentIds,
            [FromForm] string message,
            [FromForm] string notificationType)
        {
            if (studentIds == null || !studentIds.Any() || string.IsNullOrWhiteSpace(message))
                return Json(new { success = false, error = "بيانات غير مكتملة" });

            var validIds = studentIds.Where(id => id > 0).ToList();
            if (!validIds.Any())
                return Json(new { success = false, error = "لم يتم تحديد طلاب صالحين" });

            var category = notificationType switch
            {
                "طلب مراجعة" => NotificationCategory.Reminder,
                "دعوة لحضور جلسة" => NotificationCategory.Remedial,
                _ => NotificationCategory.Important
            };

            await _notificationService.SendToStudentsAsync(validIds, message, category, targetUrl: "/Student/Dashboard");
            _dashboardService.InvalidateCache();

            return Json(new { success = true, message = $"تم إرسال الإشعار بنجاح لـ {validIds.Count} طالب" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScheduleAnalyticsSession(
            [FromForm] List<int> studentIds,
            [FromForm] string sessionDate,
            [FromForm] string sessionTime,
            [FromForm] int? instructorId,
            [FromForm] string? notes)
        {
            if (studentIds == null || !studentIds.Any())
                return Json(new { success = false, error = "لم يتم تحديد طلاب" });

            var validIds = studentIds.Where(id => id > 0).ToList();
            if (!validIds.Any())
                return Json(new { success = false, error = "لم يتم تحديد طلاب صالحين" });

            var dateDisplay = string.IsNullOrWhiteSpace(sessionDate) ? "" : $" بتاريخ {sessionDate}";
            var timeDisplay = string.IsNullOrWhiteSpace(sessionTime) ? "" : $" الساعة {sessionTime}";
            var notesDisplay = string.IsNullOrWhiteSpace(notes) ? "" : $" — {notes}";
            var msg = $"تم جدولة جلسة تدارك لك{dateDisplay}{timeDisplay}{notesDisplay}. يرجى الحضور في الموعد المحدد.";

            await _notificationService.SendToStudentsAsync(validIds, msg, NotificationCategory.Remedial, targetUrl: "/Student/Dashboard");

            if (instructorId is > 0)
                await _notificationService.SendToInstructorAsync(
                    instructorId.Value,
                    $"تم تعيينك لجلسة تدارك{dateDisplay}{timeDisplay} لـ {validIds.Count} طالب.",
                    NotificationCategory.Remedial);

            _dashboardService.InvalidateCache();

            return Json(new { success = true, message = $"تمت جدولة الجلسة وإشعار {validIds.Count} طلاب" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignAnalyticsInstructor(
            [FromForm] List<int> studentIds,
            [FromForm] int instructorId,
            [FromForm] string followupType)
        {
            if (studentIds == null || !studentIds.Any())
                return Json(new { success = false, error = "لم يتم تحديد طلاب" });

            var validIds = studentIds.Where(id => id > 0).ToList();
            if (!validIds.Any())
                return Json(new { success = false, error = "لم يتم تحديد طلاب صالحين" });

            if (instructorId <= 0)
                return Json(new { success = false, error = "يرجى اختيار مدرس" });

            var instructor = await _context.Set<Instructor>().FindAsync(instructorId);
            if (instructor == null)
                return Json(new { success = false, error = "المدرس غير موجود" });

            var msg = $"تم تعيين الأستاذ {instructor.FullName} لمتابعتك ({followupType}). سيتواصل معك قريباً.";
            await _notificationService.SendToStudentsAsync(validIds, msg, NotificationCategory.Remedial, sentByInstructorId: instructorId);
            await _notificationService.SendToInstructorAsync(
                instructorId,
                $"تم تعيينك لمتابعة {validIds.Count} طالب ({followupType}).",
                NotificationCategory.Remedial);

            _dashboardService.InvalidateCache();

            return Json(new { success = true, message = $"تم تعيين الأستاذ {instructor.FullName} وإشعار {validIds.Count} طلاب" });
        }
    }
}
