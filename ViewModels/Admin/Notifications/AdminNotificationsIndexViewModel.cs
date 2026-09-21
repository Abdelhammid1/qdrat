using QdratNew.Enums;

namespace QdratNew.Areas.Admin.ViewModels
{
    public class AdminNotificationsIndexViewModel
    {
        public string SelectedCategory { get; set; } = "All";
        public bool? IsReadFilter { get; set; }
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int TodayUnreadCount { get; set; }
        public int ReadCount => TotalCount - UnreadCount;
        public List<AdminNotificationCategoryOptionViewModel> Categories { get; set; } = new();
        public List<AdminNotificationListItemViewModel> Notifications { get; set; } = new();
    }

    public class AdminNotificationListItemViewModel
    {
        public int NotificationId { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public NotificationCategory Category { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string RecipientType { get; set; } = string.Empty;
        public string? TargetUrl { get; set; }
    }

    public class AdminNotificationCategoryOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
