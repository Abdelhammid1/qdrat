namespace QdratNew.ViewModels.Admin.Shared
{
    public class AnalyticsDecisionBridgeContext
    {
        public int? BatchId { get; set; }
        public int? CurriculumId { get; set; }
        public int? InstructorId { get; set; }

        /// <summary>
        /// e.g. "high-risk-lessons", "critical-cases", "batch-performance"
        /// </summary>
        public string SourceMetric { get; set; } = "";

        /// <summary>
        /// Human-readable page name that initiated the transition.
        /// </summary>
        public string SourcePage { get; set; } = "";

        /// <summary>
        /// Local URL to redirect to when the user finishes in DecisionLab.
        /// Must pass IsLocalUrl validation before use.
        /// </summary>
        public string ReturnUrl { get; set; } = "";
    }
}
