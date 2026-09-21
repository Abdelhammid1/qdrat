using System.Threading.Tasks;
using QdratNew.ViewModels.AI;

namespace QdratNew.Services.AI
{
    public interface IAIClientService
    {
        Task<string> AnalyzeStudentAsync(AIPerformanceRequestVM model);
    }
}
