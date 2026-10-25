using QdratNew.Data.Seeders;
using QdratNew.Helpers;
using Xunit;

namespace QdratNew.Tests.CoursePackages
{
    // PKG-S6.1 — Seeder الباقات الثلاث: IsActive=false، Idempotent، بدون صور، وبيانات تجتاز الحراس
    public class CoursePackagesSeederTests
    {
        [Fact]
        public void Seed_EmptyTable_CreatesThreeInactivePackagesWithoutPosters()
        {
            var ctx = TestDbContextFactory.CreateFreshContext(out _);

            CoursePackagesSeeder.Seed(ctx);

            var packages = ctx.CoursePackages.OrderBy(p => p.DisplayOrder).ToList();
            Assert.Equal(new[] { "package-1", "package-2", "package-3" }, packages.Select(p => p.Slug).ToArray());
            Assert.All(packages, p =>
            {
                Assert.False(p.IsActive);
                Assert.True(p.ShowOnHomePage);
                Assert.True(string.IsNullOrEmpty(p.PosterImagePath));
            });
            Assert.Equal(new[] { 1, 2, 3 }, packages.Select(p => p.DisplayOrder).ToArray());
        }

        [Fact]
        public void Seed_RunTwice_DoesNotDuplicate()
        {
            var ctx = TestDbContextFactory.CreateFreshContext(out _);

            CoursePackagesSeeder.Seed(ctx);
            CoursePackagesSeeder.Seed(ctx);

            Assert.Equal(3, ctx.CoursePackages.Count());
            Assert.Equal(9, ctx.CoursePackageItems.Count());
            Assert.Equal(4, ctx.CoursePackageTags.Count());
        }

        [Fact]
        public void Seed_ExistingPackage_IsLeftUntouched()
        {
            var ctx = TestDbContextFactory.CreateFreshContext(out _);
            ctx.CoursePackages.Add(new QdratNew.Entities.Frontend.CoursePackage
            {
                Name = "باقة قائمة", Slug = "custom", GradeLabel = "x", AccentColor = "#112233", ButtonText = "احجز"
            });
            ctx.SaveChanges();

            CoursePackagesSeeder.Seed(ctx);

            Assert.Equal("custom", Assert.Single(ctx.CoursePackages).Slug);
        }

        [Fact]
        public void Seed_Data_PassesStyleGuardsAndIconCatalog()
        {
            var ctx = TestDbContextFactory.CreateFreshContext(out _);
            CoursePackagesSeeder.Seed(ctx);

            Assert.All(ctx.CoursePackages.ToList(), p =>
            {
                Assert.True(PackageStyleGuard.IsValidSlug(p.Slug), p.Slug);
                Assert.True(PackageStyleGuard.IsValidHex(p.AccentColor), p.AccentColor);
            });
            Assert.All(ctx.CoursePackageItems.ToList(), i =>
            {
                Assert.True(PackageIconCatalog.IsValidKey(i.IconKey), i.IconKey);
                Assert.True(PackageStyleGuard.IsValidHex(i.ColorHex), i.ColorHex);
            });
            Assert.All(ctx.CoursePackageTags.ToList(), t =>
                Assert.True(string.IsNullOrEmpty(t.IconKey) || PackageIconCatalog.IsValidKey(t.IconKey), t.IconKey));
        }
    }
}
