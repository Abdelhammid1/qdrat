namespace QdratNew.ViewModels.Parents
{
    public class ParentWeeklyReportViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string WeekRange { get; set; } = string.Empty;
        public string CommitmentSummary { get; set; } = string.Empty;
        public int CompletedHomeworks { get; set; }
        public int LateHomeworks { get; set; }
        public string? ExamSummary { get; set; }
        public double? BestScore { get; set; }
        public string ImprovementNote { get; set; } = string.Empty;
        public string WeekRecommendation { get; set; } = string.Empty;
        public string PlatformActionSummary { get; set; } = string.Empty;
        public string CommitmentColor { get; set; } = "success";
        public string CommitmentIcon { get; set; } = "fa-check-circle";
    }
}
