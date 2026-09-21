using Microsoft.Extensions.Caching.Memory;
using QdratNew.Services.Common;

public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public CacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public T GetOrCreate<T>(string key, Func<T> factory, int minutes = 10)
    {
        if (_cache.TryGetValue(key, out T value))
            return value;

        value = factory();

        _cache.Set(key, value, TimeSpan.FromMinutes(minutes));

        return value;
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
    }
}