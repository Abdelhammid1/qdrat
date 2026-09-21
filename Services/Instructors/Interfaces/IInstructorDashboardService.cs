using QdratNew.ViewModels.Instructor;

namespace QdratNew.Services.Instructors.Interfaces
{
    public interface IInstructorDashboardService
    {
        Task<InstructorDashboardViewModel> BuildAsync(int instructorId);
    }
}
