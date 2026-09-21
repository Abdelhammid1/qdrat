namespace QdratNew.ViewModels.Parents
{
    public class ParentStudentInsightViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string OverallStatus { get; set; } = "مستقر";
        public string StatusColor { get; set; } = "success";
        public string CommitmentStatus { get; set; } = "جيد";
        public string LearningTrend { get; set; } = "ثابت";
        public string TrendIcon { get; set; } = "fa-minus";
        public string TrendColor { get; set; } = "text-secondary";
        public string? SafeWeaknessSummary { get; set; }
        public string RecommendedAction { get; set; } = "لا توجد توصية حالياً";
        public bool CanRequestSmartPractice { get; set; } = true;
        public string? BlockingReason { get; set; }
        public int CommitmentPercent { get; set; }
    }
}
