using System.Collections.Generic;

namespace QdratNew.ViewModels.Parents
{
    public class ParentExamFollowUpViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public double? LastExamScore { get; set; }
        public string PerformanceTrend { get; set; } = "ثابت";
        public string TrendIcon { get; set; } = "fa-minus";
        public string TrendColor { get; set; } = "text-secondary";
        public bool HasUpcomingExam { get; set; }
        public string? UpcomingExamDate { get; set; }
        public bool NeedsBoostPractice { get; set; }
        public string? SafeRecommendation { get; set; }
        public List<ExamResultSummary> RecentResults { get; set; } = new();
    }

    public class ExamResultSummary
    {
        public string ExamTitle { get; set; } = string.Empty;
        public double Score { get; set; }
        public string ScoreColor { get; set; } = "secondary";
        public string ExamDate { get; set; } = string.Empty;
        public string Trend { get; set; } = "ثابت";
    }
}
