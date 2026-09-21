namespace QdratNew.ViewModels.Admin.Analytics
{
    public class MetricDecisionDetailsViewModel
    {
        public string MetricKey { get; set; } = string.Empty;
        public string MetricTitle { get; set; } = string.Empty;
        public string MetricDescription { get; set; } = string.Empty;
        public string DecisionMeaning { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
        public string RiskColor { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
        public string ExpectedImpact { get; set; } = string.Empty;
        public string PrimaryTarget { get; set; } = string.Empty;
        public string SuggestedExecutionPath { get; set; } = string.Empty;

        public int? BatchId { get; set; }
        public int? CurriculumId { get; set; }
        public int? InstructorId { get; set; }

        public bool CanOpenDecisionLab { get; set; }
        public string DecisionLabUrl { get; set; } = string.Empty;

        public List<string> AvailableActions { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
