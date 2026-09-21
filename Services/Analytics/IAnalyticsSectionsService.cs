using QdratNew.ViewModels.Admin.Analytics;
using QdratNew.ViewModels.Analytics;

namespace QdratNew.Services.Analytics
{
    public interface IAnalyticsSectionsService
    {
        /// <summary>
        /// Loads only filter dropdowns — instant response for the page shell.
        /// </summary>
        Task<AnalyticsShellVM> GetShellAsync(
            int? curriculumId, int? batchId, int? instructorId);

        /// <summary>
        /// Builds the full dashboard VM using SQL-level GROUP BY aggregations.
        /// No full-table scans — only aggregated rows are transferred to the app.
        /// </summary>
        Task<AdvancedAnalyticsDashboardVM> BuildAsync(
            int? curriculumId, int? batchId, int? instructorId, int page);

        void InvalidateCache();
    }
}
