using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Services.Partner.Interfaces
{
    public interface IPartnerExamInsightsService
    {
        Task<ExamDashboardVM> GetDashboardAsync(int partnerId);
    }
}
