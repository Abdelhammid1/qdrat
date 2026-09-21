using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QdratNew.Attributes;
using QdratNew.Enums;
using QdratNew.Interfaces;

namespace QdratNew.Filters
{
    public class PartnerFeatureFilter : IActionFilter
    {
        private readonly IPartnerSubscriptionService _subscriptionService;

        public PartnerFeatureFilter(IPartnerSubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var attribute = context.ActionDescriptor.EndpointMetadata
                .OfType<PartnerFeatureAttribute>()
                .FirstOrDefault();

            if (attribute == null)
                return;

            var partnerId = context.HttpContext.Session.GetInt32("ActivePartnerId");

            if (!partnerId.HasValue)
            {
                context.Result = new RedirectToActionResult(
                    "SelectPartner",
                    "Partners",
                    new { area = "Partner" }
                );
                return;
            }

            bool allowed = attribute.Feature switch
            {
                PartnerFeatureType.Homework =>
                    _subscriptionService.CanUseHomework(partnerId.Value),

                PartnerFeatureType.Exams =>
                    _subscriptionService.CanUseExams(partnerId.Value),

                PartnerFeatureType.PlacementExams =>
                    _subscriptionService.CanUsePlacementExams(partnerId.Value),

                PartnerFeatureType.PerformanceIndicatorExams =>
                    _subscriptionService.CanUsePerformanceIndicatorExams(partnerId.Value),

                PartnerFeatureType.ReinforcementSkills =>
                    _subscriptionService.CanUseReinforcementSkills(partnerId.Value),

                PartnerFeatureType.RemedialPlans =>
                    _subscriptionService.CanUseRemedialPlans(partnerId.Value),

                PartnerFeatureType.RemedialSessions =>
                    _subscriptionService.CanUseRemedialSessions(partnerId.Value),

                PartnerFeatureType.EducationalContent =>
                    _subscriptionService.CanAccessEducationalContent(partnerId.Value),

                PartnerFeatureType.AIAnalytics =>
                    _subscriptionService.CanUseAIAnalytics(partnerId.Value),

                _ => false
            };

            if (!allowed)
            {
                context.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/FeatureNotAllowed.cshtml",
                    ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
         new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
         context.ModelState)
                    {
                        ["FeatureName"] = attribute.Feature
                    }
                };

            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
