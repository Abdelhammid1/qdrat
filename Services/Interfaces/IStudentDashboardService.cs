using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Interfaces
{
    public interface IStudentDashboardService
    {
        Task<StudentMainDashboardVm> GetDashboardDataAsync(int studentId);
    }
}
