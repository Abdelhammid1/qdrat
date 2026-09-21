using System.Threading.Tasks;
using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Interfaces
{
    public interface IAIAnalysisService
    {
        Task<StudentAIAnalysisResultViewModel?> GetAnalysisForStudentAsync(int studentId);
    }
}
