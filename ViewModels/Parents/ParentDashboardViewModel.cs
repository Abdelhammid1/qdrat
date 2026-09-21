using System.Collections.Generic;

namespace QdratNew.ViewModels.Parents
{
    public class ParentDashboardViewModel
    {
        public int? SelectedStudentId { get; set; }
        public List<ParentChildCardViewModel> Children { get; set; } = new();
        public ParentStudentInsightViewModel? CurrentInsight { get; set; }
        public string BestActionNow { get; set; } = "اقرأ التقرير الأسبوعي";
        public string BestActionUrl { get; set; } = "/Parents/Reports/Weekly";
        public bool HasInstitutePlan { get; set; }
        public string? InstitutePlanSummary { get; set; }
        public double InstitutePlanProgress { get; set; }
        public List<ParentNotificationItemViewModel> RecentNotifications { get; set; } = new();
        public int UnreadNotificationCount { get; set; }
        public string ParentName { get; set; } = string.Empty;
    }

    public class ParentNotificationItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? TargetUrl { get; set; }
        public bool IsRead { get; set; }
        public string TimeAgo { get; set; } = string.Empty;
        public string IconClass { get; set; } = "fa-bell";
        public string BadgeColor { get; set; } = "primary";
    }
}
