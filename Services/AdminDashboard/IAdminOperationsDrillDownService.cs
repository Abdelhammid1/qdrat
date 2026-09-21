using System.Threading.Tasks;
using QdratNew.ViewModels.Dashboard;

namespace QdratNew.Services.AdminDashboard
{
    public interface IAdminOperationsDrillDownService
    {
        Task<AdminDrillDownPageViewModel> GetActiveStudentsAsync();

        Task<AdminDrillDownPageViewModel> GetRunningLecturesAsync();

        Task<AdminDrillDownPageViewModel> GetOpenExamsAsync();

        Task<AdminDrillDownPageViewModel> GetTodayAttendanceAsync();

        Task<AdminDrillDownPageViewModel> GetClosingHomeworksAsync();

        Task<AdminDrillDownPageViewModel> GetBatchHealthAsync(int? batchId);

        Task<AdminDrillDownPageViewModel> GetInstructorActivityAsync(int? instructorId);

        Task<AdminDrillDownPageViewModel> GetSmartAlertsReferenceAsync();
    }
}