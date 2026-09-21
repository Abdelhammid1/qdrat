using QdratNew.ViewModels.Analytics;

namespace QdratNew.Services.Analytics.Interfaces
{
    public interface IRemedialEngine
    {
        Task<List<RemedialActionVM>> GeneratePlanAsync(WeakPointFilter filter);
    }
}