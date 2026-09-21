namespace QdratNew.ViewModels.Parents
{
    public class ParentLearningStatusViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string CommitmentLevel { get; set; } = "جيد";
        public int CommitmentPercent { get; set; }
        public string PerformanceTrend { get; set; } = "ثابت";
        public string TrendIcon { get; set; } = "fa-minus";
        public string TrendColor { get; set; } = "text-secondary";
        public string? WeaknessSummary { get; set; }
        public string? StrengthSummary { get; set; }
        public string OverallStatus { get; set; } = "مستقر";
        public string StatusColor { get; set; } = "success";
        public int TotalHomeworks { get; set; }
        public int CompletedHomeworks { get; set; }
        public int LateHomeworks { get; set; }
        public double LastExamScore { get; set; }
        public bool HasUpcomingExam { get; set; }
        public string? UpcomingExamDate { get; set; }
    }
}
