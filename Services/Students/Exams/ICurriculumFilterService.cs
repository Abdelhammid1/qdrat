using System.Collections.Generic;
using System.Threading.Tasks;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Services.Exams
{
    public interface ICurriculumFilterService
    {
        Task<List<CurriculumFilterViewModel>> GetStudentCurriculumsAsync(
            int studentId,
            int batchId
        );
    }
}
