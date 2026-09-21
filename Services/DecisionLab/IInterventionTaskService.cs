using QdratNew.ViewModels.Admin.DecisionLab;

namespace QdratNew.Services.DecisionLab
{
    public interface IInterventionTaskService
    {
        Task<CreateInterventionTaskViewModel?> BuildCreateModelAsync(int decisionRecommendationId);
        Task<CreateInterventionTaskViewModel?> RebuildCreateModelAsync(CreateInterventionTaskViewModel model);
        Task<int> CreateAsync(CreateInterventionTaskViewModel model, string createdByUserId);
        Task<InterventionTaskListViewModel> GetListAsync();
        Task<InterventionTaskDetailsViewModel?> GetDetailsAsync(int id);
        Task<int?> GetTaskIdForRecommendationAsync(int decisionRecommendationId);
    }
}
