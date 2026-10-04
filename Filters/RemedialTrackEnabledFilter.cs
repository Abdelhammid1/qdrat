using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QdratNew.Services.RemedialTracks;

namespace QdratNew.Filters
{
    /// <summary>
    /// RTK-S7.4: عند تعطيل RemedialTrack.Enabled ترجع كل أكشنات الطالب 404 مع رسالة صيانة
    /// (صفحة للطلبات العادية، JSON لنداءات fetch) دون المساس بالبيانات.
    /// </summary>
    public sealed class RemedialTrackEnabledFilter : IAsyncActionFilter
    {
        public const string MaintenanceMessage = "الخطة العلاجية متوقفة مؤقتًا للصيانة. سيتم إعلامك فور عودتها.";

        private readonly IRemedialTrackFeatureService _feature;

        public RemedialTrackEnabledFilter(IRemedialTrackFeatureService feature) => _feature = feature;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (await _feature.IsEnabledAsync(context.HttpContext.RequestAborted))
            {
                await next();
                return;
            }

            var req = context.HttpContext.Request;
            var wantsJson = (req.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false)
                            || (req.Headers.Accept.ToString().Contains("json", StringComparison.OrdinalIgnoreCase));

            if (wantsJson)
            {
                context.Result = new ObjectResult(new { ok = false, reason = "disabled", message = MaintenanceMessage })
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
                return;
            }

            context.Result = new ViewResult
            {
                ViewName = "Maintenance",
                StatusCode = StatusCodes.Status404NotFound,
                ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                    new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                    context.ModelState)
            };
        }
    }
}
