using QdratNew.ViewModels.Remedial;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IPerformanceComparisonService
    {
        Task<List<WeakSectionVm>> AnalyzeWeakSectionsAsync(int studentId, int curriculumId);
        Task<List<StudentPerformanceComparisonVm>> AnalyzeStudentSectionPerformanceAsync(int studentId, int sectionId);
    }
}
