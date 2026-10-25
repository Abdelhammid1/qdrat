using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using QdratNew.Enums;
using QdratNew.Services.Frontend.Packages;
using QdratNew.ViewComponents;
using QdratNew.ViewModels.Admin.CoursePackages;
using Xunit;

namespace QdratNew.Tests.CoursePackages
{
    // PKG-S5.3/S5.4 — صندوق طلبات الباقات + شارة الجديد
    public class CoursePackageRegistrationAdminServiceTests
    {
        private static (ApplicationDbContext ctx, IMemoryCache cache, CoursePackageRegistrationAdminService svc) Create()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var ctx = new ApplicationDbContext(options);
            return (ctx, cache, new CoursePackageRegistrationAdminService(ctx, cache));
        }

        private static async Task<(int a, int b)> SeedAsync(ApplicationDbContext ctx, int extraForA = 0)
        {
            var a = new CoursePackage { Name = "باقة أ", Slug = "a", GradeLabel = "g", AccentColor = "#F59E0B", ButtonText = "احجز" };
            var b = new CoursePackage { Name = "باقة ب", Slug = "b", GradeLabel = "g", AccentColor = "#F59E0B", ButtonText = "احجز" };
            ctx.CoursePackages.AddRange(a, b);
            await ctx.SaveChangesAsync();

            var now = DateTime.UtcNow;
            ctx.CoursePackageRegistrations.AddRange(
                new CoursePackageRegistration { CoursePackageId = a.Id, PackageNameSnapshot = a.Name, FullName = "سارة محمد", PhoneNumber = "0501111111", SubmittedAt = now.AddMinutes(-3) },
                new CoursePackageRegistration { CoursePackageId = b.Id, PackageNameSnapshot = b.Name, FullName = "خالد", PhoneNumber = "0502222222", Status = CoursePackageRegistrationStatus.Contacted, SubmittedAt = now.AddMinutes(-2) });
            for (var i = 0; i < extraForA; i++)
                ctx.CoursePackageRegistrations.Add(new CoursePackageRegistration
                {
                    CoursePackageId = a.Id, PackageNameSnapshot = a.Name, FullName = "عميل " + i, PhoneNumber = "05" + (30000000 + i), SubmittedAt = now.AddMinutes(-100 - i)
                });
            await ctx.SaveChangesAsync();
            return (a.Id, b.Id);
        }

        [Fact]
        public async Task Index_FiltersByStatusPackageAndSearch_AndCountsAll()
        {
            var (ctx, _, svc) = Create();
            var (a, b) = await SeedAsync(ctx);

            var all = await svc.GetIndexAsync(new CoursePackageRegistrationFilter());
            Assert.Equal(2, all.TotalItems);
            Assert.Equal(1, all.Counts.NewCount);
            Assert.Equal(1, all.Counts.ContactedCount);
            Assert.Equal(2, all.Packages.Count);

            var byStatus = await svc.GetIndexAsync(new CoursePackageRegistrationFilter { Status = CoursePackageRegistrationStatus.Contacted });
            Assert.Equal("خالد", Assert.Single(byStatus.Rows).FullName);

            var byPackage = await svc.GetIndexAsync(new CoursePackageRegistrationFilter { PackageId = a });
            Assert.Equal("سارة محمد", Assert.Single(byPackage.Rows).FullName);

            var byPhone = await svc.GetIndexAsync(new CoursePackageRegistrationFilter { Search = "٠٥٠٢٢٢" });
            Assert.Equal("خالد", Assert.Single(byPhone.Rows).FullName);
        }

        [Fact]
        public async Task Index_Paginates20PerPage_AndClampsPage()
        {
            var (ctx, _, svc) = Create();
            await SeedAsync(ctx, extraForA: 23); // 25 إجمالًا

            var p2 = await svc.GetIndexAsync(new CoursePackageRegistrationFilter { Page = 2 });
            Assert.Equal(25, p2.TotalItems);
            Assert.Equal(2, p2.TotalPages);
            Assert.Equal(5, p2.Rows.Count);

            var clamped = await svc.GetIndexAsync(new CoursePackageRegistrationFilter { Page = 99 });
            Assert.Equal(2, clamped.Filter.Page);
        }

        [Fact]
        public async Task Update_ToContacted_SetsContactedAtHandlerAndClearsBadgeCache()
        {
            var (ctx, cache, svc) = Create();
            await SeedAsync(ctx);
            cache.Set(NewPackageRegistrationsBadgeViewComponent.CacheKey, 1);
            var id = ctx.CoursePackageRegistrations.First(r => r.Status == CoursePackageRegistrationStatus.New).Id;

            var ok = await svc.UpdateAsync(new UpdateCoursePackageRegistrationRequest
            {
                Id = id, Status = CoursePackageRegistrationStatus.Contacted, AdminNotes = "  تم الاتصال  "
            }, "user-1");

            Assert.True(ok);
            var reg = ctx.CoursePackageRegistrations.Single(r => r.Id == id);
            Assert.Equal(CoursePackageRegistrationStatus.Contacted, reg.Status);
            Assert.NotNull(reg.ContactedAt);
            Assert.Equal("user-1", reg.HandledByUserId);
            Assert.Equal("تم الاتصال", reg.AdminNotes);
            Assert.NotNull(reg.LastUpdatedAt);
            Assert.False(cache.TryGetValue(NewPackageRegistrationsBadgeViewComponent.CacheKey, out _));
        }

        [Fact]
        public async Task Update_MissingRegistration_ReturnsFalse()
        {
            var (_, _, svc) = Create();
            Assert.False(await svc.UpdateAsync(new UpdateCoursePackageRegistrationRequest { Id = 999, Status = CoursePackageRegistrationStatus.New }, null));
        }

        [Fact]
        public async Task GetDetails_ReturnsSnapshotAndNull()
        {
            var (ctx, _, svc) = Create();
            await SeedAsync(ctx);
            var id = ctx.CoursePackageRegistrations.First().Id;

            var vm = await svc.GetDetailsAsync(id);
            Assert.NotNull(vm);
            Assert.StartsWith("966", vm!.WhatsAppNumber);
            Assert.Null(await svc.GetDetailsAsync(12345));
        }

        [Fact]
        public async Task Badge_CountsOnlyNewRegistrations_AndCaches()
        {
            var (ctx, cache, _) = Create();
            await SeedAsync(ctx);

            var vc = new NewPackageRegistrationsBadgeViewComponent(ctx, cache);
            await vc.InvokeAsync();

            Assert.True(cache.TryGetValue(NewPackageRegistrationsBadgeViewComponent.CacheKey, out int count));
            Assert.Equal(1, count);
        }
    }
}
