using Microsoft.Extensions.Caching.Memory;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.RemedialTracks
{
    public sealed class RemedialTrackFeatureService : IRemedialTrackFeatureService
    {
        public const string SettingKey = "RemedialTrack.Enabled";
        private const string CacheKey = "rtk-feature-enabled";
        private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

        private readonly ISystemSettingService _settings;
        private readonly IMemoryCache _cache;

        public RemedialTrackFeatureService(ISystemSettingService settings, IMemoryCache cache)
        {
            _settings = settings;
            _cache = cache;
        }

        /// <summary>الإعداد الغائب أو غير الصالح = مفعّلة (الافتراضي true)؛ فقط القيمة الصريحة false تعطّل الميزة.</summary>
        public static bool Parse(string? value)
            => !(bool.TryParse(value?.Trim(), out var parsed) && !parsed);

        public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheKey, out bool cached)) return cached;

            // GetAsync لا يسجّل تحذيرًا عند غياب المفتاح (بخلاف GetValue) ولا يكتب في كاش الإعدادات العام
            var enabled = Parse(await _settings.GetAsync(SettingKey));
            _cache.Set(CacheKey, enabled, CacheFor);
            return enabled;
        }
    }
}
