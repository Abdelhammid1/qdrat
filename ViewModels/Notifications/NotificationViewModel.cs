namespace QdratNew.ViewModels.Notifications
{
    public class NotificationViewModel
    {
        public string? StudentName { get; set; }
        public string Message { get; set; }
        public string Category { get; set; }
        public DateTime SentAt { get; set; }
        public string? TargetUrl { get; set; }
    }
}
