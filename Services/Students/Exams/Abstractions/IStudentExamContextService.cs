using QdratNew.ViewModels.Exam;

namespace QdratNew.Services.Students.Exams.Abstractions
{
    public interface IStudentExamContextService
    {
        Task<List<StudentExamCardViewModel>> GetAllAsync(
            int studentId,
            int courseId,
            int batchId);

        Task<List<StudentExamCardViewModel>> GetRequiredAsync(
            int studentId,
            int courseId,
            int batchId);

        Task<List<StudentExamCardViewModel>> GetCompletedAsync(
            int studentId,
            int courseId,
            int batchId);

        Task<List<StudentExamCardViewModel>> GetLateAsync(
            int studentId,
            int courseId,
            int batchId);
    }
}
