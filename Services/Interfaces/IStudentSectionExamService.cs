using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IStudentSectionExamService
    {
        Task CheckAndGenerateExamAsync(int studentId, int sectionId);
    }
}
