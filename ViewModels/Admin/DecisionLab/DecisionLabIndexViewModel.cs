namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class DecisionLabIndexViewModel
    {
        public int? SelectedBatchId { get; set; }
        public int? SelectedCurriculumId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        public List<DecisionLabBatchAnalysisViewModel> Batches { get; set; } = new();
        public DecisionLabBatchAnalysisViewModel? SelectedBatchAnalysis { get; set; }

        public int TotalStudentsCount { get; set; }
        public int TotalAtRiskCount { get; set; }
        public int AtRiskChangeThisWeek { get; set; }
        public double OverallAvgScorePct { get; set; }
        public double AvgScoreChangePct { get; set; }
        public double HomeworkCompletionPct { get; set; }
        public double CompletionChangeThisMonth { get; set; }
        public int ApprovedRecommendationsCount { get; set; }
        public int PendingRecommendationsCount { get; set; }
        public int ActiveInterventionTasksCount { get; set; }

        public string? SelectedBatchName { get; set; }
        public DateTime LastUpdatedAt { get; set; }

        // Collections
        public List<RiskDistributionItem>? RiskDistribution { get; set; }
        public List<DecisionLabCriticalIssueViewModel>? CriticalIssues { get; set; }
        public List<LessonCompletionItem>? LessonCompletions { get; set; }
        public List<RecentRecommendationItem>? RecentRecommendations { get; set; }

        // Chart data
        public List<string> WeeklyLabels { get; set; } = new();
        public List<double> WeeklyAvgScores { get; set; } = new();
        public List<double> WeeklyCompletionRates { get; set; } = new();
        public List<int> WeeklyFailingCounts { get; set; } = new();
    }

    public class RiskDistributionItem
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
        public string Color { get; set; } = "#ccc";
    }

    public class DecisionLabCriticalIssueViewModel
    {
        public int Id { get; set; }
        public int BatchId { get; set; }
        public string SeverityCss { get; set; } = string.Empty;
        public string SeverityLabel { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int AffectedStudentsCount { get; set; }
        public string MetricLabel { get; set; } = string.Empty;
        public string MetricValue { get; set; } = string.Empty;
        public string TrendDirection { get; set; } = string.Empty;
        public string TrendText { get; set; } = string.Empty;
    }

    public class LessonCompletionItem
    {
        public string LessonTitle { get; set; } = string.Empty;
        public double CompletionPct { get; set; }
    }

    public class RecentRecommendationItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TasksCount { get; set; }
    }
}
