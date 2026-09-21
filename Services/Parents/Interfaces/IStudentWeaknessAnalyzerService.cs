using System.Threading.Tasks;
using QdratNew.ViewModels.Parents;

namespace QdratNew.Services.Parents.Interfaces
{
    public interface IStudentWeaknessAnalyzerService
    {
        Task<StudentWeaknessAnalysisResult> AnalyzeAsync(int studentId, int? curriculumId, int? sectionId);
    }
}
