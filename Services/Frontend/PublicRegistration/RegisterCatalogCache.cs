using Microsoft.Extensions.Caching.Memory;

namespace QdratNew.Services.Frontend.PublicRegistration;

/// <summary>
/// مفتاح كاش كتالوج صفحة التسجيل العامة (RL). يُستخدم لإبطال الكاش بعد أي تعديل
/// على مفاتيح الإظهار في Projects / Courses. خدمة الكتالوج (RL-S4.1) ستقرأ/تكتب بنفس المفتاح.
/// </summary>
public static class RegisterCatalogCache
{
    public const string Key = "RL:RegisterCatalog";

    public static void Invalidate(IMemoryCache cache) => cache.Remove(Key);
}
