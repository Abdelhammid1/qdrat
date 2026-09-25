using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace QdratNew.Services.Frontend.HomePage;

/// <summary>
/// مفاتيح كاش محتوى الواجهة العامة (الصفحة الرئيسية + الـ navbar + أقسام الشهادات والمجموعات).
/// الإبطال يتم تلقائيًا من HomePageCacheInvalidationInterceptor بعد أي SaveChanges على الجداول المعنية.
/// </summary>
public static class HomePageCache
{
    public const string ContentKey = "HP:HomeContent";
    public const string CatalogKey = "HP:FrontendCatalog";
    public const string ProfCertSectionKey = "HP:ProfCertSection";
    public const string CourseCollectionsSectionKey = "HP:CourseCollectionsSection";

    /// <summary>محتوى يتغيّر من لوحة الأدمن فقط — شبكة أمان في حال فات إبطال.</summary>
    public static readonly TimeSpan ContentTtl = TimeSpan.FromMinutes(10);

    /// <summary>أقسام فيها عدّادات تسجيل — مدة أقصر.</summary>
    public static readonly TimeSpan CountersTtl = TimeSpan.FromSeconds(60);

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public static void InvalidateContent(IMemoryCache cache)
    {
        cache.Remove(ContentKey);
        cache.Remove(CatalogKey);
    }

    public static void InvalidateAll(IMemoryCache cache)
    {
        InvalidateContent(cache);
        cache.Remove(ProfCertSectionKey);
        cache.Remove(CourseCollectionsSectionKey);
    }

    /// <summary>
    /// GetOrCreate غير متزامن بقفل لكل مفتاح: عند انتهاء الكاش تحت ضغط 50 طلب متزامن
    /// يذهب طلب واحد فقط لقاعدة البيانات، والباقي ينتظر النتيجة.
    /// القفل لكل مفتاح (لا قفل عام) حتى لا يحدث deadlock عند تداخل مفتاحين.
    /// </summary>
    public static async Task<T> GetOrLoadAsync<T>(
        IMemoryCache cache, string key, TimeSpan ttl, Func<Task<T>> factory) where T : class
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var gate = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (cache.TryGetValue(key, out cached) && cached is not null)
                return cached;

            var value = await factory();
            cache.Set(key, value, ttl);
            return value;
        }
        finally
        {
            gate.Release();
        }
    }
}
