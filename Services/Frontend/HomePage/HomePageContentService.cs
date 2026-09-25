using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using QdratNew.ViewModels.Public.HomePage;

namespace QdratNew.Services.Frontend.HomePage;

public class HomePageContentService : IHomePageContentService
{
    // نفس الـ slugs المستخدمة حاليًا في الـ partials — لا تغيّرها
    private const string AboutSlug = "about";
    private const string VisionSlug = "roytna";
    private const string MissionSlug = "rsaltna";
    private const string SaudiVisionSlug = "mnsh-alqdrat-wroyh-almmlkh-2030";
    private const string PetrojetCompany = "petrojet";

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public HomePageContentService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<FrontendNavViewModel> GetNavigationAsync()
    {
        var catalog = await GetCatalogAsync();

        return new FrontendNavViewModel
        {
            MainLinks = catalog.Where(c => c.ShowAsMainLink).OrderBy(c => c.DisplayOrder).ToList(),
            NavbarCourses = catalog.Where(c => c.ShowInNavbar).OrderBy(c => c.DisplayOrder).ToList(),
            SubCoursesByCourse = catalog
                .SelectMany(c => c.SubCourses ?? Enumerable.Empty<SubCourse>())
                .OrderBy(s => s.DisplayOrder)
                .ToLookup(s => s.FrontendCourseId)
        };
    }

    public Task<HomePageContentViewModel> GetHomeContentAsync() =>
        HomePageCache.GetOrLoadAsync(_cache, HomePageCache.ContentKey, HomePageCache.ContentTtl, LoadHomeContentAsync);

    // ─── التحميل الفعلي (يُنفَّذ فقط عند غياب الكاش) ─────────────────────────

    /// <summary>
    /// كل الدورات الفعّالة مع الـ SubCourses الفعّالة — مصدر واحد لقسم البرامج والـ navbar.
    /// </summary>
    private Task<List<FrontendCourse>> GetCatalogAsync() =>
        HomePageCache.GetOrLoadAsync(_cache, HomePageCache.CatalogKey, HomePageCache.ContentTtl, async () =>
            await _context.FrontendCourses
                .AsNoTracking()
                .Include(c => c.SubCourses.Where(s => s.IsActive).OrderBy(s => s.DisplayOrder))
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .ToListAsync());

    private async Task<HomePageContentViewModel> LoadHomeContentAsync()
    {
        // استعلامات متتالية عمدًا — DbContext غير آمن للتوازي
        var heroSlides = await _context.HeroSlides
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

        // 4 صفحات في استعلام واحد (شروط OR صريحة — بدون Contains على قائمة)
        var pages = await _context.StaticPages
            .AsNoTracking()
            .Where(p => p.IsActive &&
                        (p.Slug == AboutSlug || p.Slug == VisionSlug ||
                         p.Slug == MissionSlug || p.Slug == SaudiVisionSlug))
            .ToListAsync();

        var successPartners = await _context.SuccessPartners
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        var goals = await _context.PlatformGoals
            .AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync();

        var petrojet = await _context.CorporateRegistrationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyName == PetrojetCompany);

        var catalog = await GetCatalogAsync();

        return new HomePageContentViewModel
        {
            HeroSlides = heroSlides,
            ProgramsCourses = catalog,
            AboutPage = pages.FirstOrDefault(p => p.Slug == AboutSlug),
            VisionPage = pages.FirstOrDefault(p => p.Slug == VisionSlug),
            MissionPage = pages.FirstOrDefault(p => p.Slug == MissionSlug),
            SaudiVisionPage = pages.FirstOrDefault(p => p.Slug == SaudiVisionSlug),
            SuccessPartners = successPartners,
            PlatformGoals = goals,
            // نفس منطق _PartnersSection الحالي بالضبط
            ShowPetrojetPartnerSection = petrojet == null || petrojet.IsVisibleOnHomePage
        };
    }
}
