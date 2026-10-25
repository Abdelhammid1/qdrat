using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Enums;

namespace QdratNew.ViewComponents
{
    // PKG-S5.4 — شارة عدد طلبات حجز الباقات الجديدة في القائمة الجانبية.
    // مُخزَّنة في IMemoryCache لمدة 60 ثانية (نفس نمط NewLeadsBadge).
    public class NewPackageRegistrationsBadgeViewComponent : ViewComponent
    {
        public const string CacheKey = "pkg:new-registrations-badge";

        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public NewPackageRegistrationsBadgeViewComponent(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!_cache.TryGetValue(CacheKey, out int count))
            {
                count = await _context.CoursePackageRegistrations
                    .AsNoTracking()
                    .CountAsync(r => r.Status == CoursePackageRegistrationStatus.New);

                _cache.Set(CacheKey, count, TimeSpan.FromSeconds(60));
            }

            return View(count);
        }
    }
}
