using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.Services.Analytics
{
    public interface IMetricDecisionService
    {
        DecisionMetricDetailsVM BuildMetricDecisionDetails(
             string metric,
             AdvancedAnalyticsDashboardVM dashboard);
    }
}
