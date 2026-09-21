using QdratNew.Services.Exams.Models;
using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Services.Instructors.Exams.Interfaces
{
    public interface IInstructorExamDraftService
    {
        int SaveDraft(GeneratedExamResult generated, int instructorId);

        ExamDraftPreviewVM GetDraft(int draftId, int instructorId);
    }
}