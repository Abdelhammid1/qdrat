using QdratNew.ViewModels.Reports;

namespace QdratNew.Services.Interfaces
{
    public interface IPerformanceReportService
    {
        Task<List<IndicatorPerformanceReportViewModel>> GetBatchPerformanceReportAsync(int batchId);

        Task<(int TotalStudents, int Passed, int Failed, int WithRemedial)> GetBatchStatisticsAsync(int batchId);



    }
}
