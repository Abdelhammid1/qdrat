using QdratNew.ViewModels.Dashboard;
using QdratNew.ViewModels.Remedial;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IPerformanceDashboardService
    {
        Task<PerformanceDashboardViewModel> GetDashboardDataAsync();
        Task<List<BatchPerformanceItem>> GetActiveBatchPerformanceAsync();
        Task<BatchPerformanceDetailViewModel> GetBatchPerformanceDetailsAsync(int batchId);
        Task<ExamAnalyticsViewModel?> GetExamAnalyticsAsync(int examId);
        Task<StudentRemedialAnalysisViewModel?> BuildStudentRemedialAnalysisAsync(int studentId, int examId);

        Task<List<BatchExamItem>> GetExamsForBatchAsync(int batchId);
        Task<ExamPerformanceDetailViewModel?> GetExamPerformanceDetailsAsync(int examId);

    }
}
