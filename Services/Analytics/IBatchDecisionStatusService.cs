using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.Services.Analytics
{
    public interface IBatchDecisionStatusService
    {
        Task<Dictionary<int, BatchDecisionStatusVM>> GetStatusForBatchesAsync(IEnumerable<int> batchIds);
    }
}
