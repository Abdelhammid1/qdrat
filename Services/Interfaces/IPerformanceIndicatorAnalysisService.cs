using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IPerformanceIndicatorAnalysisService
    {
        Task GenerateRemedialPlansAsync(int performanceExamId);
    }
}
