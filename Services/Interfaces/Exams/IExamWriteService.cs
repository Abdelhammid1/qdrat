using QdratNew.DTOs.Exams;

namespace QdratNew.Services.Interfaces.Exams
{
    public interface IExamWriteService
    {
        Task SaveQuestionAttemptAsync(ExamQuestionAttemptInput input);
        Task SubmitExamAsync(int examAssignmentId, int studentId);
    }
}
