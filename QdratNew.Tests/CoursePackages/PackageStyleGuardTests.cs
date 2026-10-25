using QdratNew.Helpers;
using Xunit;

namespace QdratNew.Tests.CoursePackages
{
    // PKG-S1.5 — حارسا الألوان/الـ slugs وكتالوج الأيقونات
    public class PackageStyleGuardTests
    {
        [Theory]
        [InlineData("#F59E0B")]
        [InlineData("#14a8ae")]
        [InlineData("#000000")]
        public void IsValidHex_AcceptsRrggbb(string value) => Assert.True(PackageStyleGuard.IsValidHex(value));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("F59E0B")]
        [InlineData("#FFF")]
        [InlineData("#GGGGGG")]
        [InlineData("#F59E0B1")]
        [InlineData("red")]
        [InlineData("#F59E0B;background:url(x)")]
        [InlineData("#F59E0B\n")]
        public void IsValidHex_RejectsInvalid(string? value) => Assert.False(PackageStyleGuard.IsValidHex(value));

        [Theory]
        [InlineData("package-1")]
        [InlineData("a")]
        [InlineData("summer-2026-pack")]
        public void IsValidSlug_AcceptsLatinSlugs(string value) => Assert.True(PackageStyleGuard.IsValidSlug(value));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Package-1")]
        [InlineData("package_1")]
        [InlineData("-package")]
        [InlineData("package-")]
        [InlineData("pack--age")]
        [InlineData("package-1\n")]
        [InlineData("باقة")]
        [InlineData("admin")]
        [InlineData("register")]
        [InlineData("success")]
        [InlineData("thanks")]
        [InlineData("new")]
        public void IsValidSlug_RejectsInvalidOrReserved(string? value) => Assert.False(PackageStyleGuard.IsValidSlug(value));

        [Fact]
        public void IsValidSlug_RejectsTooLong() => Assert.False(PackageStyleGuard.IsValidSlug(new string('a', 161)));

        [Fact]
        public void ReadableTextColor_DarkBackground_ReturnsWhite()
        {
            Assert.Equal("#FFFFFF", PackageStyleGuard.ReadableTextColor("#1B7C8A"));
            Assert.Equal("#FFFFFF", PackageStyleGuard.ReadableTextColor("#000000"));
        }

        [Fact]
        public void ReadableTextColor_LightBackground_ReturnsDarkInk()
        {
            Assert.Equal("#1F2937", PackageStyleGuard.ReadableTextColor("#F59E0B"));
            Assert.Equal("#1F2937", PackageStyleGuard.ReadableTextColor("#FFFFFF"));
        }

        [Fact]
        public void ReadableTextColor_InvalidInput_FallsBackToWhite() =>
            Assert.Equal("#FFFFFF", PackageStyleGuard.ReadableTextColor("not-a-color"));

        [Fact]
        public void IconCatalog_KeysAreUniqueAndComplete()
        {
            Assert.Equal(PackageIconCatalog.All.Count, PackageIconCatalog.All.Select(x => x.Key).Distinct().Count());
            Assert.All(PackageIconCatalog.All, i =>
            {
                Assert.False(string.IsNullOrWhiteSpace(i.Label));
                Assert.StartsWith("fas fa-", i.FaClass);
                Assert.StartsWith("bi bi-", i.BiClass);
            });
        }

        [Theory]
        [InlineData("bullseye")]
        [InlineData("female")]
        public void IconCatalog_IsValidKey_AcceptsKnown(string key) => Assert.True(PackageIconCatalog.IsValidKey(key));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Bullseye")]
        [InlineData("fas fa-bullseye")]
        [InlineData("<script>")]
        public void IconCatalog_IsValidKey_RejectsUnknown(string? key) => Assert.False(PackageIconCatalog.IsValidKey(key));

        [Fact]
        public void IconCatalog_Resolve_UnknownFallsBackToBook()
        {
            Assert.Equal("book", PackageIconCatalog.Resolve("nope").Key);
            Assert.Equal("book", PackageIconCatalog.Resolve(null).Key);
            Assert.Equal("trophy", PackageIconCatalog.Resolve("trophy").Key);
        }
    }
}
