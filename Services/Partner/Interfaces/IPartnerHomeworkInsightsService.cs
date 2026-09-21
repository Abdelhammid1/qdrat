using QdratNew.ViewModels.Partner.Homework;

namespace QdratNew.Services.Partner.Interfaces
{
    public interface IPartnerHomeworkInsightsService
    {
        Task<HomeworkDashboardVM> GetDashboardAsync(int partnerId);
    }
}