using QdratNew.DTOs.Homework;

namespace QdratNew.Services.Homework.Interfaces
{
    public interface IHomeworkWriteService
    {
        Task SaveQuestionAttemptAsync(HomeworkQuestionAttemptInput input);
        Task SubmitHomeworkAsync(int homeworkSetId, int studentId);
    }
}
