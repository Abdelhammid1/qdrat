using QdratNew.ViewModels.Remedial;

namespace QdratNew.Services.Interfaces
{
    public interface IRemedialPlanBuilderService
    {
        Task<int> GenerateSessionAsync(RemedialResourceSelectionVm model);
    }
}
