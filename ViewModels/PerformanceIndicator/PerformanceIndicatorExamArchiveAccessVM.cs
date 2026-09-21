using System.Collections.Generic;

namespace QdratNew.ViewModels.PerformanceIndicator
{
    public class PerformanceIndicatorExamArchiveAccessVM
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public List<PerformanceIndicatorArchiveUserAccessItem> Users { get; set; } = new();
    }

    public class PerformanceIndicatorArchiveUserAccessItem
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public bool IsAllowed { get; set; }
    }
}
