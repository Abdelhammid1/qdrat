using QdratNew.ViewModels.Dashboard;
using System.Threading.Tasks;

namespace QdratNew.Services.AdminDashboard
{
    public interface IAdminOperationsDashboardService
    {
        Task<AdminOperationsDashboardViewModel> GetDashboardAsync();

        Task<AdminDashboardPulseViewModel> GetPulseAsync();
    }
}