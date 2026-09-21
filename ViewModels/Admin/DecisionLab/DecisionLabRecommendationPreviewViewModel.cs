namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class DecisionLabRecommendationPreviewViewModel
    {
        public int? BatchId { get; set; }
        public int? CurriculumId { get; set; }

        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string InterventionType { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;
        public string WarningMessage { get; set; } = string.Empty;

        public int ImpactScore { get; set; }
        public bool RequiresApproval { get; set; }
        public bool CanCreateExamDraft { get; set; }
        public int? SavedRecommendationId { get; set; }
        public DecisionRecommendationSourceContextViewModel SourceContext { get; set; } = new();
    }
}
