using QdratNew.ViewModels.Reports;

namespace QdratNew.Services.Interfaces
{
    public interface IBatchPerformanceRecommendationService
    {
        List<BatchRecommendationVM> GenerateRecommendations(BatchPerformanceReportVM report);
        List<string> BuildWeeklyActionPlan(BatchPerformanceReportVM report);
    }
}
