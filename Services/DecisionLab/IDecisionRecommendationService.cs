using QdratNew.ViewModels.Admin.DecisionLab;

namespace QdratNew.Services.DecisionLab
{
    public interface IDecisionRecommendationService
    {
        Task<IReadOnlyList<DecisionLabRecommendationPreviewViewModel>> BuildRecommendationsAsync(
            DecisionLabBatchAnalysisViewModel batchAnalysis);

        Task<DecisionLabRecommendationDetailsViewModel> BuildRecommendationDetailsAsync(
            DecisionLabBatchAnalysisViewModel batchAnalysis,
            string recommendationCode);
    }
}
