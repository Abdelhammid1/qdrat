using Microsoft.Extensions.Caching.Memory;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>OLC-S1.3: بنمط RemedialTrackFeatureService (كاش 30 ثانية) لكن مغلق افتراضيًا.</summary>
    public sealed class OnlineLectureFeatureService : IOnlineLectureFeatureService
    {
        public const string SettingKey = "OnlineLectureContent.Enabled";
        private const string CacheKey = "olc-feature-enabled";
        private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

        private readonly ISystemSettingService _settings;
        private readonly IMemoryCache _cache;

        public OnlineLectureFeatureService(ISystemSettingService settings, IMemoryCache cache)
        {
            _settings = settings;
            _cache = cache;
        }

        /// <summary>فقط القيمة الصريحة true تفعّل الميزة؛ الغائب أو غير الصالح = مغلقة (الافتراضي false).</summary>
        public static bool Parse(string? value)
            => bool.TryParse(value?.Trim(), out var parsed) && parsed;

        public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheKey, out bool cached)) return cached;

            // GetAsync لا يسجّل تحذيرًا عند غياب المفتاح ولا يكتب في كاش الإعدادات العام
            var enabled = Parse(await _settings.GetAsync(SettingKey));
            _cache.Set(CacheKey, enabled, CacheFor);
            return enabled;
        }
    }
}
