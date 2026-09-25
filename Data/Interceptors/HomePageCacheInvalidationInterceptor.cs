using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Entities;
using QdratNew.Entities.Frontend;
using QdratNew.Services.Frontend.HomePage;

namespace QdratNew.Data.Interceptors;

/// <summary>
/// يبطل كاش الواجهة العامة بعد أي حفظ يمس جداولها — بدل تعديل 8 كنترولرات أدمن.
/// يُبطل قبل الحفظ (حتى لا يُقدَّم محتوى قديم) وبعد نجاحه (حتى لا يعيد طلب متزامن تعبئة الكاش بالقيمة القديمة).
/// Singleton — بلا حالة سوى جدول ضعيف لكل DbContext.
/// </summary>
public sealed class HomePageCacheInvalidationInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<Type> ContentTypes = new()
    {
        typeof(HeroSlide), typeof(SuccessPartner), typeof(StaticPage),
        typeof(CorporateRegistrationSetting),
        typeof(PlatformGoal), typeof(FrontendCourse), typeof(SubCourse)
    };

    private static readonly HashSet<Type> ProfCertTypes = new()
    {
        typeof(ProfessionalCertificateSectionSetting), typeof(ProfessionalCertificateCourse),
        typeof(ProfessionalCertificateRegistration)
    };

    private static readonly HashSet<Type> CourseCollectionTypes = new()
    {
        typeof(CourseCollection), typeof(CourseCollectionCourse),
        typeof(CourseCollectionSponsor), typeof(CourseCollectionRegistration)
    };

    private readonly IMemoryCache _cache;
    private readonly ConditionalWeakTable<DbContext, HashSet<string>> _pending = new();

    public HomePageCacheInvalidationInterceptor(IMemoryCache cache) => _cache = cache;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Flush(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Flush(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Flush(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;

        var keys = new HashSet<string>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var type = entry.Metadata.ClrType;
            if (ContentTypes.Contains(type))
            {
                keys.Add(HomePageCache.ContentKey);
                keys.Add(HomePageCache.CatalogKey);
            }
            else if (ProfCertTypes.Contains(type))
            {
                keys.Add(HomePageCache.ProfCertSectionKey);
            }
            else if (CourseCollectionTypes.Contains(type))
            {
                keys.Add(HomePageCache.CourseCollectionsSectionKey);
            }
        }

        if (keys.Count == 0) return;

        foreach (var key in keys) _cache.Remove(key);
        _pending.AddOrUpdate(context, keys);
    }

    private void Flush(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var keys)) return;

        foreach (var key in keys) _cache.Remove(key);
        _pending.Remove(context);
    }
}
