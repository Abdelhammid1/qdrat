using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Data.Interceptors;
using QdratNew.Entities;
using QdratNew.Services.Frontend.HomePage;
using Xunit;

namespace QdratNew.Tests
{
    // HP-S3.2 — الكاش يخدم الطلب الثاني بلا استعلامات، والحفظ عبر EF يبطله فورًا
    public class HomePageCacheInvalidationTests
    {
        private static (ApplicationDbContext ctx, IMemoryCache cache, HomePageContentService svc) Create()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(new HomePageCacheInvalidationInterceptor(cache))
                .Options;
            var ctx = new ApplicationDbContext(options);
            return (ctx, cache, new HomePageContentService(ctx, cache));
        }

        private static HeroSlide NewSlide() => new()
        {
            Title = "old", Description = "d", ButtonText = "b", ButtonUrl = "/", ImagePath = "i.png",
            IsActive = true, DisplayOrder = 1
        };

        [Fact]
        public async Task GetHomeContent_SecondCall_ReturnsSameCachedInstance()
        {
            var (ctx, _, svc) = Create();
            ctx.HeroSlides.Add(NewSlide());
            await ctx.SaveChangesAsync();

            var first = await svc.GetHomeContentAsync();
            var second = await svc.GetHomeContentAsync();

            Assert.Same(first, second);
        }

        [Fact]
        public async Task SaveChanges_OnHeroSlide_InvalidatesContentAndCatalogKeys()
        {
            var (ctx, cache, svc) = Create();
            var slide = NewSlide();
            ctx.HeroSlides.Add(slide);
            await ctx.SaveChangesAsync();

            var before = await svc.GetHomeContentAsync();
            await svc.GetNavigationAsync();
            Assert.Equal("old", before.HeroSlides[0].Title);
            Assert.True(cache.TryGetValue(HomePageCache.ContentKey, out _));
            Assert.True(cache.TryGetValue(HomePageCache.CatalogKey, out _));

            slide.Title = "new";
            await ctx.SaveChangesAsync();

            Assert.False(cache.TryGetValue(HomePageCache.ContentKey, out _));
            Assert.False(cache.TryGetValue(HomePageCache.CatalogKey, out _));

            var after = await svc.GetHomeContentAsync();
            Assert.NotSame(before, after);
            Assert.Equal("new", after.HeroSlides[0].Title);
        }

        [Fact]
        public async Task SaveChanges_OnUnrelatedEntity_KeepsCache()
        {
            var (ctx, cache, svc) = Create();
            await svc.GetHomeContentAsync();

            ctx.FrontendLeads.Add(new QdratNew.Entities.Frontend.FrontendLead
            {
                StudentName = "x",
                PhoneNumber = "0500000000",
                SelectedProgram = ""
            });
            await ctx.SaveChangesAsync();

            Assert.True(cache.TryGetValue(HomePageCache.ContentKey, out _));
        }
    }
}
