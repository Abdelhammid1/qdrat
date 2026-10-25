using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Data.Interceptors;
using QdratNew.Entities.Frontend;
using QdratNew.Services.Frontend.HomePage;
using QdratNew.Services.Frontend.Packages;
using Xunit;

namespace QdratNew.Tests.CoursePackages
{
    // PKG-S4 — الخدمة العامة للباقات + إبطال كاش الرئيسية
    public class CoursePackagePublicServiceTests
    {
        private static (ApplicationDbContext ctx, IMemoryCache cache, CoursePackagePublicService svc) Create()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(new HomePageCacheInvalidationInterceptor(cache))
                .Options;
            var ctx = new ApplicationDbContext(options);
            return (ctx, cache, new CoursePackagePublicService(ctx, cache));
        }

        private static CoursePackage NewPackage(string slug, bool active = true, bool home = true, int order = 1) => new()
        {
            Name = "باقة " + slug, Slug = slug, GradeLabel = "للصف الثالث الثانوي", AccentColor = "#F59E0B",
            ButtonText = "احجز الآن", IsActive = active, ShowOnHomePage = home, DisplayOrder = order,
            Items =
            {
                new CoursePackageItem { Title = "ب", IconKey = "trophy", DisplayOrder = 2, ScheduleText = "تاريخ" },
                new CoursePackageItem { Title = "أ", IconKey = "unknown-key", DisplayOrder = 1, ColorHex = "javascript:x", ScheduleTitle = "عنوان الجدول" }
            },
            Tags = { new CoursePackageTag { Text = "وسم", IconKey = "male", DisplayOrder = 1 } }
        };

        [Fact]
        public async Task GetBySlug_InactivePackage_ReturnsNull()
        {
            var (ctx, _, svc) = Create();
            ctx.CoursePackages.Add(NewPackage("p-off", active: false));
            await ctx.SaveChangesAsync();

            Assert.Null(await svc.GetBySlugAsync("p-off"));
            Assert.Null(await svc.GetBySlugAsync("missing"));
        }

        [Fact]
        public async Task GetBySlug_MapsItemsOrderedSafely()
        {
            var (ctx, _, svc) = Create();
            ctx.CoursePackages.Add(NewPackage("p-1"));
            await ctx.SaveChangesAsync();

            var vm = await svc.GetBySlugAsync("p-1");

            Assert.NotNull(vm);
            Assert.Equal(new[] { "أ", "ب" }, vm!.Items.Select(i => i.Title).ToArray());
            Assert.Equal(new[] { 1, 2 }, vm.Items.Select(i => i.Number).ToArray());
            Assert.Equal("عنوان الجدول", vm.Items[0].ScheduleTitle);
            Assert.Equal("ب", vm.Items[1].ScheduleTitle);          // fallback إلى Title
            Assert.Equal("bi bi-book", vm.Items[0].IconBiClass);    // مفتاح مجهول → book
            Assert.Null(vm.Items[0].ColorHex);                      // لون غير صالح لا يمر
            Assert.Equal("bi bi-person-fill", vm.Tags[0].IconBiClass);
            Assert.Equal("#1F2937", vm.OnAccentColor);              // لون فاتح → نص داكن
        }

        [Fact]
        public async Task GetHomeSection_FiltersOrdersAndCapsAtSix()
        {
            var (ctx, _, svc) = Create();
            for (var i = 1; i <= 8; i++) ctx.CoursePackages.Add(NewPackage($"p-{i}", order: i));
            ctx.CoursePackages.Add(NewPackage("hidden", home: false, order: 0));
            ctx.CoursePackages.Add(NewPackage("off", active: false, order: 0));
            await ctx.SaveChangesAsync();

            var list = await svc.GetHomeSectionAsync();

            Assert.Equal(6, list.Count);
            Assert.Equal("p-1", list[0].Slug);
            Assert.DoesNotContain(list, p => p.Slug is "hidden" or "off");
            Assert.All(list, p => Assert.Equal(2, p.Items.Count));
        }

        [Fact]
        public async Task GetAllActive_IncludesHiddenFromHomeButNotInactive()
        {
            var (ctx, _, svc) = Create();
            ctx.CoursePackages.Add(NewPackage("a", order: 1));
            ctx.CoursePackages.Add(NewPackage("b", home: false, order: 2));
            ctx.CoursePackages.Add(NewPackage("c", active: false, order: 3));
            await ctx.SaveChangesAsync();

            var list = await svc.GetAllActiveAsync();

            Assert.Equal(new[] { "a", "b" }, list.Select(p => p.Slug).ToArray());
        }

        [Fact]
        public async Task GetHomeSection_SecondCall_ReturnsCachedInstance()
        {
            var (ctx, _, svc) = Create();
            ctx.CoursePackages.Add(NewPackage("p-1"));
            await ctx.SaveChangesAsync();

            var first = await svc.GetHomeSectionAsync();
            var second = await svc.GetHomeSectionAsync();

            Assert.Same(first, second);
        }

        [Fact]
        public async Task SaveChanges_OnPackageOrItem_InvalidatesPackagesKey()
        {
            var (ctx, cache, svc) = Create();
            var pkg = NewPackage("p-1");
            ctx.CoursePackages.Add(pkg);
            await ctx.SaveChangesAsync();

            await svc.GetHomeSectionAsync();
            Assert.True(cache.TryGetValue(HomePageCache.PackagesSectionKey, out _));

            pkg.Name = "جديد";
            await ctx.SaveChangesAsync();
            Assert.False(cache.TryGetValue(HomePageCache.PackagesSectionKey, out _));

            await svc.GetHomeSectionAsync();
            pkg.Items.First().Title = "عنوان جديد";
            await ctx.SaveChangesAsync();
            Assert.False(cache.TryGetValue(HomePageCache.PackagesSectionKey, out _));
        }

        [Fact]
        public async Task SaveChanges_OnRegistration_DoesNotInvalidatePackagesKey()
        {
            var (ctx, cache, svc) = Create();
            var pkg = NewPackage("p-1");
            ctx.CoursePackages.Add(pkg);
            await ctx.SaveChangesAsync();

            await svc.GetHomeSectionAsync();
            ctx.CoursePackageRegistrations.Add(new CoursePackageRegistration
            {
                CoursePackageId = pkg.Id, PackageNameSnapshot = pkg.Name, FullName = "اسم", PhoneNumber = "0500000000"
            });
            await ctx.SaveChangesAsync();

            Assert.True(cache.TryGetValue(HomePageCache.PackagesSectionKey, out _));
        }

        [Fact]
        public async Task GetBySlug_ItemOnColor_IsReadableAndFallsBackForInvalidColor()
        {
            var (ctx, _, svc) = Create();
            var pkg = NewPackage("p-1");
            pkg.Items.Add(new CoursePackageItem { Title = "فاتح", IconKey = "book", DisplayOrder = 3, ColorHex = "#FACC15" });
            pkg.Items.Add(new CoursePackageItem { Title = "داكن", IconKey = "book", DisplayOrder = 4, ColorHex = "#1B3A57" });
            ctx.CoursePackages.Add(pkg);
            await ctx.SaveChangesAsync();

            var vm = await svc.GetBySlugAsync("p-1");

            Assert.Equal("#1F2937", vm!.Items.Single(i => i.Title == "فاتح").OnColorHex);
            Assert.Equal("#FFFFFF", vm.Items.Single(i => i.Title == "داكن").OnColorHex);
            Assert.Equal("#FFFFFF", vm.Items.Single(i => i.Title == "أ").OnColorHex); // لون غير صالح → الافتراضي
        }

        private static QdratNew.ViewModels.Frontend.Packages.PackageRegisterFormViewModel Form(string name, string phone) =>
            new() { FullName = name, PhoneNumber = phone };

        [Fact]
        public async Task Submit_Valid_SavesRegistrationWithSnapshotAndNormalizedPhone()
        {
            var (ctx, _, svc) = Create();
            ctx.CoursePackages.Add(NewPackage("p-1"));
            await ctx.SaveChangesAsync();

            var result = await svc.SubmitAsync("p-1", Form("  أحمد علي  ", "٠٥٠٠٠٠٠٠٠٠"), "1.2.3.4");

            Assert.True(result.Success);
            var reg = Assert.Single(ctx.CoursePackageRegistrations);
            Assert.Equal("أحمد علي", reg.FullName);
            Assert.Equal("0500000000", reg.PhoneNumber);
            Assert.Equal("باقة p-1", reg.PackageNameSnapshot);
            Assert.Equal(QdratNew.Enums.CoursePackageRegistrationStatus.New, reg.Status);
        }

        [Fact]
        public async Task Submit_InvalidPhoneOrShortName_ReturnsErrorsAndSavesNothing()
        {
            var (ctx, _, svc) = Create();
            ctx.CoursePackages.Add(NewPackage("p-1"));
            await ctx.SaveChangesAsync();

            var result = await svc.SubmitAsync("p-1", Form("ab", "12345"), null);

            Assert.False(result.Success);
            Assert.Contains("FullName", result.Errors.Keys);
            Assert.Contains("PhoneNumber", result.Errors.Keys);
            Assert.Empty(ctx.CoursePackageRegistrations);
        }

        [Fact]
        public async Task Submit_InactiveOrMissingPackage_ReturnsNotFound()
        {
            var (ctx, _, svc) = Create();
            ctx.CoursePackages.Add(NewPackage("p-off", active: false));
            await ctx.SaveChangesAsync();

            Assert.True((await svc.SubmitAsync("p-off", Form("أحمد علي", "0500000000"), null)).NotFound);
            Assert.True((await svc.SubmitAsync("missing", Form("أحمد علي", "0500000000"), null)).NotFound);
            Assert.Empty(ctx.CoursePackageRegistrations);
        }

        [Fact]
        public async Task Submit_DuplicateWithinWindow_SucceedsSilentlyWithoutNewRow()
        {
            var (ctx, _, svc) = Create();
            ctx.CoursePackages.Add(NewPackage("p-1"));
            await ctx.SaveChangesAsync();

            await svc.SubmitAsync("p-1", Form("أحمد علي", "0500000000"), null);
            var second = await svc.SubmitAsync("p-1", Form("أحمد علي", "0500000000"), null);

            Assert.True(second.Success);
            Assert.Single(ctx.CoursePackageRegistrations);
        }
    }
}
