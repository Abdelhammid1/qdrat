using QdratNew.Entities;

namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class DecisionRecommendationDetailsPageViewModel
    {
        public DecisionRecommendation Recommendation { get; set; } = new();
        public bool CanCreateInterventionTask { get; set; }
        public int? ExistingInterventionTaskId { get; set; }
        public InterventionTaskDetailsViewModel? ExistingInterventionTask { get; set; }
    }
}
