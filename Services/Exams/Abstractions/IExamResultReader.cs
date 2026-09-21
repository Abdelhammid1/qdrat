using QdratNew.DTOs.Exams;
using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Abstractions
{
    public interface IExamResultReader
    {
        Task<ExamFinalResultDto?> GetResultAsync(
            int examAssignmentId,
            int studentId
        );
    }

}
