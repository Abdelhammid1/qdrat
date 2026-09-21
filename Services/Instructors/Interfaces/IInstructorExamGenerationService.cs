using QdratNew.Services.Exams.Models;
using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Services.Instructors.Exams.Interfaces
{
    public interface IInstructorExamGenerationService
    {
        GeneratedExamResult GeneratePreview(
            GenerateExamRequestVM request,
            int instructorId);
    }
}