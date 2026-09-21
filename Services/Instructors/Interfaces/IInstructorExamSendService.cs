using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Services.Instructors.Exams.Interfaces
{
    public interface IInstructorExamSendService
    {
        void SendDraftToBatches(SendExamDraftVM model, int instructorId);
    }
}