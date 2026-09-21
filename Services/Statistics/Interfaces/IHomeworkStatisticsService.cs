using QdratNew.Services.Statistics.DTOs;

namespace QdratNew.Services.Statistics.Interfaces
{
    public interface IHomeworkStatisticsService
    {
        HomeworkDraftStatsDto GetPartnerDraftStats(
            int partnerId,
            int subscriptionPeriodId
        );
    }
}
