using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;

namespace QdratNew.ViewComponents
{
    /// <summary>RTK-S4.1: بند «الخطة العلاجية» في قائمة الطالب — يظهر فقط لمن لديه تسجيل نشط ظاهر.</summary>
    public class StudentRemedialTrackNavViewComponent : ViewComponent
    {
        private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

        private readonly IStudentIdentityService _identity;
        private readonly IRemedialTrackAccessService _access;
        private readonly IMemoryCache _cache;
        private readonly IRemedialTrackFeatureService _feature;

        public StudentRemedialTrackNavViewComponent(
            IStudentIdentityService identity, IRemedialTrackAccessService access, IMemoryCache cache,
            IRemedialTrackFeatureService feature)
        {
            _identity = identity;
            _access = access;
            _cache = cache;
            _feature = feature;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (HttpContext.User.Identity?.IsAuthenticated != true) return Content(string.Empty);
            if (!await _feature.IsEnabledAsync(HttpContext.RequestAborted)) return Content(string.Empty);   // RTK-S7.4

            var studentId = await _identity.GetCurrentStudentIdAsync(HttpContext.User);
            if (studentId == 0) return Content(string.Empty);

            var visible = await _cache.GetOrCreateAsync($"rtk-nav:{studentId}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheFor;
                return await _access.HasVisibleEnrollmentAsync(studentId, HttpContext.RequestAborted);
            });

            return visible ? View() : Content(string.Empty);
        }
    }
}
