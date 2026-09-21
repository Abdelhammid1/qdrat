namespace QdratNew.Services.DecisionLab
{
    using QdratNew.ViewModels.Admin.DecisionLab;

    public interface IDecisionRecommendationApprovalService
    {
        Task<int> SaveForReviewAsync(
            int batchId,
            int? curriculumId,
            string? recommendationCode,
            string requestedByUserId,
            DecisionRecommendationSourceContextViewModel? sourceContext = null);

        Task ApproveAsync(int id, string approvedByUserId);
    }
}
