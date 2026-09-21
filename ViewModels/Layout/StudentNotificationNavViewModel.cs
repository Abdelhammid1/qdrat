namespace QdratNew.ViewModels.Layout
{
    public class StudentNotificationNavViewModel
    {
        public int TotalUnreadCount { get; set; }
        public int HomeworkUnreadCount { get; set; }
        public int ExamUnreadCount { get; set; }
        public int IndividualExamUnreadCount { get; set; }
        public IReadOnlyList<StudentNotificationNavItemViewModel> RecentNotifications { get; set; } = [];
    }
}
