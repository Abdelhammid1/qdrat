using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.Services.Analytics
{
    public interface IAnalyticsDashboardService
    {
        Task<AdvancedAnalyticsDashboardVM> BuildDashboardAsync(
            int? curriculumId, int? batchId, int? instructorId, int page);

        void InvalidateCache();
    }
}
