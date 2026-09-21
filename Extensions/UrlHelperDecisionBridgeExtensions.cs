using Microsoft.AspNetCore.Mvc;
using QdratNew.ViewModels.Admin.Shared;

namespace QdratNew.Extensions
{
    public static class UrlHelperDecisionBridgeExtensions
    {
        public static string ToDecisionLabUrl(
            this IUrlHelper urlHelper,
            AnalyticsDecisionBridgeContext ctx,
            string action = "RecommendationPreview")
        {
            var safeReturnUrl = IsLocalUrl(ctx.ReturnUrl) ? ctx.ReturnUrl : null;

            return urlHelper.Action(action, "DecisionLab", new
            {
                area           = "Admin",
                batchId        = ctx.BatchId,
                curriculumId   = ctx.CurriculumId,
                sourceArea       = "Admin",
                sourceController = "Analytics",
                sourceAction     = "AdvancedDashboard",
                sourceMetric     = ctx.SourceMetric,
                sourcePage       = ctx.SourcePage,
                returnUrl        = safeReturnUrl
            }) ?? "#";
        }

        public static string ToBatchAnalysisUrl(
            this IUrlHelper urlHelper,
            AnalyticsDecisionBridgeContext ctx)
        {
            return urlHelper.Action("BatchAnalysis", "DecisionLab", new
            {
                area         = "Admin",
                batchId      = ctx.BatchId,
                curriculumId = ctx.CurriculumId
            }) ?? "#";
        }

        // Mirrors the ASP.NET Core Controller.IsLocalUrl check.
        private static bool IsLocalUrl(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            return url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
        }
    }
}
