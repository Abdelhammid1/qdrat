using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;

namespace QdratNew.ViewComponents
{
    // RL-S1.3 — شارة عدد طلبات الالتحاق الجديدة (لم يتم التواصل معها) في القائمة الجانبية.
    // مُخزَّنة في IMemoryCache لمدة 60 ثانية حتى لا يُنفَّذ استعلام مع كل تحميل صفحة أدمن.
    public class NewLeadsBadgeViewComponent : ViewComponent
    {
        public const string CacheKey = "rl:new-leads-badge";

        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public NewLeadsBadgeViewComponent(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!_cache.TryGetValue(CacheKey, out int count))
            {
                count = await _context.FrontendLeads
                    .AsNoTracking()
                    .CountAsync(l => !l.IsContacted);

                _cache.Set(CacheKey, count, TimeSpan.FromSeconds(60));
            }

            return View(count);
        }
    }
}
