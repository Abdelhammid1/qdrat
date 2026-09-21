using QdratNew.ViewModels.Admin.EmployeeDashboard;
using System.Security.Claims;

namespace QdratNew.Services.Admin.EmployeeDashboard
{
    public interface IEmployeeDashboardService
    {
        Task<EmployeeDashboardViewModel> GetDashboardAsync(ClaimsPrincipal user);
    }
}
