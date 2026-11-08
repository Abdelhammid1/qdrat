using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Areas.Students.Controllers;
using QdratNew.Services.Interfaces;
using QdratNew.Services.OnlineLecture;

namespace QdratNew.ViewComponents
{
    /// <summary>
    /// OLC-S5.1 (R3): بند «المحتوى التعليمي» في قائمة الطالب — يظهر فقط مع المفتاح المفعّل ولمن له تسجيل نشط في دفعة
    /// أونلاين متاحة ضمن الكورس النشط (من Session السياق؛ بلا سياق = أي كورس). كاش لكل طالب+كورس يُبطَل فورًا
    /// بـ OnlineLearningMenuCacheSignal عند تفعيل/إلغاء دفعة أو حفظ عقد شريك، وعمره 60ث كحد أقصى لبقية التغييرات
    /// (التسجيلات، حالة الطالب، انتهاء العقد بمرور اليوم) — نفس عمر كاش رابط الخطة العلاجية.
    /// الإخفاء هنا ليس حماية: OnlineLearningController يعيد الفحص في كل Action.
    /// </summary>
    public class LearningContentMenuViewComponent : ViewComponent
    {
        private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);
        private const string CourseContextSessionKey = "StudentCourseContext";

        private readonly IStudentIdentityService _identity;
        private readonly IOnlineLearningAccessService _access;
        private readonly IOnlineLectureFeatureService _feature;
        private readonly IMemoryCache _cache;

        public LearningContentMenuViewComponent(
            IStudentIdentityService identity,
            IOnlineLearningAccessService access,
            IOnlineLectureFeatureService feature,
            IMemoryCache cache)
        {
            _identity = identity;
            _access = access;
            _feature = feature;
            _cache = cache;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (HttpContext.User.Identity?.IsAuthenticated != true) return Content(string.Empty);
            if (!await _feature.IsEnabledAsync(HttpContext.RequestAborted)) return Content(string.Empty);

            var studentId = await _identity.GetCurrentStudentIdAsync(HttpContext.User);
            if (studentId == 0) return Content(string.Empty);

            var courseId = ReadActiveCourseId();

            var visible = await _cache.GetOrCreateAsync($"olc-nav:{studentId}:{courseId ?? 0}", async entry =>
            {
                // الرمز يُلتقط قبل الاستعلام: أي إبطال أثناءه يُسقط هذه النتيجة فورًا
                entry.AddExpirationToken(OnlineLearningMenuCacheSignal.Token);
                entry.AbsoluteExpirationRelativeToNow = CacheFor;
                return await _access.IsEligibleAsync(studentId, courseId, HttpContext.RequestAborted);
            });

            return visible ? View() : Content(string.Empty);
        }

        private int? ReadActiveCourseId()
        {
            if (HttpContext.Items.TryGetValue("ActiveCourseId", out var item) && item is int fromItems && fromItems > 0)
                return fromItems;

            try
            {
                var json = HttpContext.Session.GetString(CourseContextSessionKey);
                if (string.IsNullOrWhiteSpace(json)) return null;
                var ctx = JsonSerializer.Deserialize<StudentCourseContextVm>(json);
                return ctx is { CourseId: > 0 } ? ctx.CourseId : null;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                return null;
            }
        }
    }
}
