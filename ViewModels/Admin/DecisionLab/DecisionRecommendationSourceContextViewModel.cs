namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class DecisionRecommendationSourceContextViewModel
    {
        public string SourceArea { get; set; } = string.Empty;
        public string SourceController { get; set; } = string.Empty;
        public string SourceAction { get; set; } = string.Empty;
        public string SourceMetric { get; set; } = string.Empty;
        public string SourcePage { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public DateTime CapturedAt { get; set; }
    }
}
