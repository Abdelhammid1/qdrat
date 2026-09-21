using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Admin.Notifications
{
    public class CreateNotificationViewModel
    {
        [Required(ErrorMessage = "يجب تحديد نوع المستلمين")]
        public string TargetType { get; set; } = "Students";

        [Required(ErrorMessage = "الرسالة مطلوبة")]
        [MinLength(5, ErrorMessage = "الرسالة يجب أن تكون 5 أحرف على الأقل")]
        public string Message { get; set; } = string.Empty;

        public NotificationCategory Category { get; set; } = NotificationCategory.General;

        public DateTime? MeetingAt { get; set; }

        // Admins
        public List<string> SelectedAdminUserIds { get; set; } = new();

        // Students
        public int? SelectedBatchId { get; set; }
        public List<int> SelectedStudentIds { get; set; } = new();

        // Instructors
        public List<int> SelectedInstructorIds { get; set; } = new();

        // Dropdowns / Lists (populated in GET)
        public List<AdminUserCheckboxItem> AdminUsers { get; set; } = new();
        public List<SelectListItem> Batches { get; set; } = new();
        public List<InstructorCheckboxItem> Instructors { get; set; } = new();
        public List<NotificationCategorySelectItem> Categories { get; set; } = new();
    }

    public class AdminUserCheckboxItem
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? RoleName { get; set; }
    }

    public class InstructorCheckboxItem
    {
        public int InstructorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Specialization { get; set; }
    }

    public class NotificationCategorySelectItem
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
