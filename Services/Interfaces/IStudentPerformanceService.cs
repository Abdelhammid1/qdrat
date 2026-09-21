using QdratNew.ViewModels.AI;
using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Interfaces
{
    public interface IStudentPerformanceService
    {
        Task<ChartAnalysisInputViewModel> GetChartAnalysisDataAsync(int studentId);
    }
}
