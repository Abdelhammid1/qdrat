using QdratNew.Enums;

namespace QdratNew.ViewModels.Students
{
    public class StudentNotificationViewModel
    {
        public int NotificationId { get; set; }       // يطابق Notification.NotificationId
        public string Message { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public NotificationCategory Category { get; set; }

        public string? TargetUrl { get; set; }
        public object Url { get; internal set; }





    }
}
