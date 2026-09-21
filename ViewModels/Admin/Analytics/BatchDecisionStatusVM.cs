namespace QdratNew.ViewModels.Admin.Analytics
{
    public class BatchDecisionStatusVM
    {
        public int BatchId { get; set; }
        public bool HasPendingRecommendation { get; set; }
        public bool HasApprovedRecommendation { get; set; }
        public bool HasActiveInterventionTask { get; set; }
        public int? LatestRecommendationId { get; set; }
        public DateTime? LatestDecisionAt { get; set; }

        public string StatusLabel => HasActiveInterventionTask    ? "تدخل نشط"
            : HasApprovedRecommendation                           ? "قرار معتمد"
            : HasPendingRecommendation                            ? "بانتظار الاعتماد"
            : "لا قرار";

        public string StatusCssClass => HasActiveInterventionTask ? "success"
            : HasApprovedRecommendation                           ? "primary"
            : HasPendingRecommendation                            ? "warning"
            : "secondary";
    }
}
