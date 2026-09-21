using QdratNew.ViewModels.Partner.Dashboard;

namespace QdratNew.Services.Partner.Interfaces
{
    public interface IPartnerDashboardDataService
    {
        PartnerDashboardViewModel GetDashboard(int partnerId);
    }
}
