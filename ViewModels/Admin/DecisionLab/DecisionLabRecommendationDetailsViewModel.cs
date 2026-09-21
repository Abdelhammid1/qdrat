namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class DecisionLabRecommendationDetailsViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;

        public bool RequiresApproval { get; set; }
        public bool CanCreateExamDraft { get; set; }

        public DecisionRiskBreakdownViewModel RiskBreakdown { get; set; } = new();
        public DecisionRecommendationSourceContextViewModel SourceContext { get; set; } = new();
        public List<string> EvidencePoints { get; set; } = new();
        public List<DecisionLabWeakLessonViewModel> RelatedWeakLessons { get; set; } = new();
        public List<DecisionLabHighRiskQuestionViewModel> RelatedHighRiskQuestions { get; set; } = new();
    }
}
