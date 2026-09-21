using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Interfaces.Exams
{
    public interface IStudentCourseDashboardService
    {
        Task<StudentMainDashboardVm> GetCourseDashboardAsync(
            int studentId,
            int courseId,
            int batchId
        );
    }

}
