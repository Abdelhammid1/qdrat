namespace QdratNew.ViewModels.Remedial
{
    public class RemedialDashboardVm
    {
        public string StudentName { get; set; }
        public string PlanTitle { get; set; }
        public DateTime PlanCreatedAt { get; set; }

        public int TotalVideos { get; set; }
        public int CompletedVideos { get; set; }
        public double TotalWatchMinutes { get; set; }
        public double AverageWatchPerVideo { get; set; }
        public double AverageQuizScore { get; set; }
        public double ProgressPercent { get; set; }

        public string AIAssessedLevel { get; set; }
        public string AIRecommendations { get; set; }
        public string SmartRecommendation { get; set; }
        public double AIAssessedRiskScore { get; set; }

        public string ProgressColor =>
            ProgressPercent >= 80 ? "bg-success"
            : ProgressPercent >= 50 ? "bg-warning text-dark"
            : "bg-danger text-white";

    }
}
