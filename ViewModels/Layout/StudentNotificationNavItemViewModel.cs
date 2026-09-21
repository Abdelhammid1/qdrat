using QdratNew.Enums;

namespace QdratNew.ViewModels.Layout
{
    public class StudentNotificationNavItemViewModel
    {
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public NotificationCategory Category { get; set; }
        public string? TargetUrl { get; set; }
    }
}
