using System.Collections.Generic;

namespace QdratNew.ViewModels.Parents
{
    public class ParentMonthlyReportViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string MonthLabel { get; set; } = string.Empty;
        public string OverallTrend { get; set; } = "ثابت";
        public string TrendColor { get; set; } = "secondary";
        public string TrendIcon { get; set; } = "fa-minus";
        public List<string> ImprovementPoints { get; set; } = new();
        public List<string> FollowUpPoints { get; set; } = new();
        public string PlatformImpactSummary { get; set; } = string.Empty;
        public int TotalHomeworks { get; set; }
        public int CompletedHomeworks { get; set; }
        public int TotalPracticeRequests { get; set; }
        public string MonthRecommendation { get; set; } = string.Empty;
    }
}
