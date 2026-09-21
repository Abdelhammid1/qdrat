using QdratNew.ViewModels.Homework;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IStudentHomeworkAnalyticsService
    {
        Task<HomeworkAnalyticsVm> AnalyzeHomeworkAsync(int studentId, int homeworkSetId);
        Task UpdateHomeworkSubmissionAsync(int studentId, int homeworkSetId, double score, bool isSubmitted);
        Task EnsureStudentsLinkedToHomeworkSetAsync(int homeworkSetId);

        // ✅ أضف هذه الدالة الجديدة
        Task EnsureStudentsLinkedToHomeworkSetAsyncForStudent(int studentId);
    }
}
