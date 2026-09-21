using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Implementations
{
    public class SystemSettingService : ISystemSettingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SystemSettingService> _logger;

        public SystemSettingService(ApplicationDbContext context, IMemoryCache cache, ILogger<SystemSettingService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }


        public async Task<string?> GetAsync(string key)
        {
            return await _context.SystemSettings
                .Where(s => s.Key == key)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();
        }

        public string? GetValue(string key)
        {
            if (_cache.TryGetValue($"Setting:{key}", out string cachedValue))
                return cachedValue;

            var setting = _context.SystemSettings.FirstOrDefault(s => s.Key == key);
            if (setting != null)
            {
                _cache.Set($"Setting:{key}", setting.Value, TimeSpan.FromMinutes(10));
                return setting.Value;
            }

            _logger.LogWarning($"⚠️ الإعداد '{key}' غير موجود.");
            return null;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            var value = GetValue(key);
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        public async Task<int> GetIntAsync(string key, int defaultValue = 0)
        {
            var value = await GetAsync(key);
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            var value = GetValue(key);
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        public async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
        {
            var value = await GetAsync(key);
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        public async Task<double> GetDoubleAsync(string key, double defaultValue = 0)
        {
            var value = await GetAsync(key);
            return double.TryParse(value, out double result) ? result : defaultValue;
        }

        public async Task<bool> UpdateValueAsync(string key, string value)
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null)
                return false;

            setting.Value = value;
            _cache.Remove($"Setting:{key}");

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
