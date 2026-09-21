using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Partner.Reports;

namespace QdratNew.Services.Interfaces
{
    public interface IStudentHomeworkStatusService
    {
        Task<StudentHomeworkSummaryVm> GetHomeworkSummaryAsync(
            int studentId,
            int courseId,
            int batchId);

        Task<List<HomeworkListVm>> GetAllAsync(
            int studentId,
            int courseId,
            int batchId);

        Task<List<HomeworkListVm>> GetRequiredAsync(
            int studentId,
            int courseId,
            int batchId);

        Task<List<HomeworkListVm>> GetSolvedAsync(
            int studentId,
            int courseId,
            int batchId);

        Task<List<HomeworkListVm>> GetLateAsync(
            int studentId,
            int courseId,
            int batchId);
    }
}
