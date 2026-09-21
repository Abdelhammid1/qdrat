using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.Services.Analytics
{
    public interface IInstructorAnalyticsService
    {
        Task<InstructorPerformanceDetailsVM?> BuildInstructorPerformanceAsync(int instructorId);
    }
}
