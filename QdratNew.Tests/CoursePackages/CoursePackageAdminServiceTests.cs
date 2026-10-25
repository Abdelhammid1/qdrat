using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using QdratNew.Enums;
using QdratNew.Services.Frontend.Packages;
using QdratNew.ViewModels.Admin.CoursePackages;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace QdratNew.Tests.CoursePackages
{
    // PKG-S2.4 — خدمة أدمن الباقات: إنشاء، Reconcile، فرادة الـ slug، التحقق، الحذف، الصورة
    public class CoursePackageAdminServiceTests : IDisposable
    {
        private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "pkg-tests-" + Guid.NewGuid());

        public CoursePackageAdminServiceTests() => Directory.CreateDirectory(_webRoot);

        public void Dispose()
        {
            try { Directory.Delete(_webRoot, true); } catch { }
        }

        private static (CoursePackageAdminService svc, ApplicationDbContext ctx) Create()
        {
            var ctx = TestDbContextFactory.CreateFreshContext(out _);
            return (new CoursePackageAdminService(ctx), ctx);
        }

        private static CoursePackageFormViewModel Valid(string slug = "package-1") => new()
        {
            Name = "البكج الأول",
            Slug = slug,
            GradeLabel = "للصف الثالث الثانوي",
            AccentColor = "#f59e0b",
            DiscountPercent = 51,
            ButtonText = "احجز الآن",
            Items =
            {
                new PackageItemFormViewModel { Title = "أ", IconKey = "bullseye" },
                new PackageItemFormViewModel { Title = "ب", IconKey = "trophy", ColorHex = "#14a8ae" },
                new PackageItemFormViewModel { Title = "ج", IconKey = "clock" }
            },
            Tags = { new PackageTagFormViewModel { Text = "7 أيام كمي", IconKey = "star" } }
        };

        private static IFormFile PngFile(string name = "poster.png")
        {
            using var img = new Image<Rgba32>(40, 30);
            var ms = new MemoryStream();
            img.SaveAsPng(ms);
            ms.Position = 0;
            return new FormFile(ms, 0, ms.Length, "PosterFile", name);
        }

        private static IFormFile FakeFile(string name = "poster.png")
        {
            var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
            return new FormFile(ms, 0, ms.Length, "PosterFile", name);
        }

        [Fact]
        public async Task Create_Valid_SavesWithOrderedChildrenAndNormalizedValues()
        {
            var (svc, ctx) = Create();
            var (ok, id, errors) = await svc.CreateAsync(Valid(), _webRoot, "u1");

            Assert.True(ok);
            Assert.Empty(errors);
            var p = await ctx.CoursePackages.Include(x => x.Items).Include(x => x.Tags).SingleAsync(x => x.Id == id);
            Assert.Equal("#F59E0B", p.AccentColor);
            Assert.Equal("u1", p.UpdatedByUserId);
            Assert.Equal(new[] { "أ", "ب", "ج" }, p.Items.OrderBy(i => i.DisplayOrder).Select(i => i.Title));
            Assert.Equal(new[] { 1, 2, 3 }, p.Items.Select(i => i.DisplayOrder).OrderBy(x => x));
            Assert.Equal("#14A8AE", p.Items.Single(i => i.Title == "ب").ColorHex);
            Assert.Single(p.Tags);
        }

        [Fact]
        public async Task Create_DuplicateSlug_ReturnsSlugError()
        {
            var (svc, ctx) = Create();
            await svc.CreateAsync(Valid(), _webRoot, null);

            var (ok, _, errors) = await svc.CreateAsync(Valid(), _webRoot, null);

            Assert.False(ok);
            Assert.True(errors.ContainsKey("Slug"));
            Assert.Equal(1, await ctx.CoursePackages.CountAsync());
        }

        [Fact]
        public async Task Create_InvalidIconColorOrSlug_IsRejected_NothingSaved()
        {
            var (svc, ctx) = Create();

            var badIcon = Valid("p-a"); badIcon.Items[0].IconKey = "fas fa-hack";
            var badItemColor = Valid("p-b"); badItemColor.Items[0].ColorHex = "red;x";
            var badAccent = Valid("p-c"); badAccent.AccentColor = "#GGG";
            var badSlug = Valid("Admin");
            var badTagIcon = Valid("p-d"); badTagIcon.Tags[0].IconKey = "nope";

            Assert.True((await svc.CreateAsync(badIcon, _webRoot, null)).Errors.ContainsKey("Items[0].IconKey"));
            Assert.True((await svc.CreateAsync(badItemColor, _webRoot, null)).Errors.ContainsKey("Items[0].ColorHex"));
            Assert.True((await svc.CreateAsync(badAccent, _webRoot, null)).Errors.ContainsKey("AccentColor"));
            Assert.True((await svc.CreateAsync(badSlug, _webRoot, null)).Errors.ContainsKey("Slug"));
            Assert.True((await svc.CreateAsync(badTagIcon, _webRoot, null)).Errors.ContainsKey("Tags[0].IconKey"));
            Assert.Equal(0, await ctx.CoursePackages.CountAsync());
        }

        [Fact]
        public async Task Create_ItemsCountOutOfRange_IsRejected()
        {
            var (svc, _) = Create();
            var none = Valid(); none.Items.Clear();
            var many = Valid("p-m");
            many.Items = Enumerable.Range(0, 9).Select(i => new PackageItemFormViewModel { Title = "x" + i, IconKey = "book" }).ToList();

            Assert.True((await svc.CreateAsync(none, _webRoot, null)).Errors.ContainsKey("Items"));
            Assert.True((await svc.CreateAsync(many, _webRoot, null)).Errors.ContainsKey("Items"));
        }

        [Fact]
        public async Task Update_Reconcile_AddsUpdatesAndRemovesChildren()
        {
            var (svc, ctx) = Create();
            var (_, id, _) = await svc.CreateAsync(Valid(), _webRoot, null);
            var form = (await svc.GetFormAsync(id))!;

            var kept = form.Items.Single(i => i.Title == "ب");
            kept.Title = "ب معدّل";
            form.Items = new List<PackageItemFormViewModel>
            {
                new() { Title = "جديد", IconKey = "puzzle" },   // إضافة (أول الترتيب)
                kept                                            // تحديث
                // "أ" و"ج" غائبتان → حذف
            };
            form.Tags.Clear();

            var (found, errors) = await svc.UpdateAsync(form, _webRoot, "u2");

            Assert.True(found);
            Assert.Empty(errors);
            var p = await ctx.CoursePackages.Include(x => x.Items).Include(x => x.Tags).SingleAsync();
            Assert.Equal(new[] { "جديد", "ب معدّل" }, p.Items.OrderBy(i => i.DisplayOrder).Select(i => i.Title));
            Assert.Equal(kept.Id, p.Items.Single(i => i.Title == "ب معدّل").Id);
            Assert.Empty(p.Tags);
            Assert.Equal("u2", p.UpdatedByUserId);
            Assert.NotNull(p.UpdatedAt);
        }

        [Fact]
        public async Task Update_SlugOfAnotherPackage_IsRejected_ButOwnSlugAllowed()
        {
            var (svc, _) = Create();
            await svc.CreateAsync(Valid("package-1"), _webRoot, null);
            var (_, id2, _) = await svc.CreateAsync(Valid("package-2"), _webRoot, null);

            var form = (await svc.GetFormAsync(id2))!;
            var (_, same) = await svc.UpdateAsync(form, _webRoot, null);
            Assert.Empty(same);

            form.Slug = "package-1";
            var (found, errors) = await svc.UpdateAsync(form, _webRoot, null);
            Assert.True(found);
            Assert.True(errors.ContainsKey("Slug"));
        }

        [Fact]
        public async Task Update_ItemIdFromAnotherPackage_IsRejected()
        {
            var (svc, ctx) = Create();
            await svc.CreateAsync(Valid("package-1"), _webRoot, null);
            var (_, id2, _) = await svc.CreateAsync(Valid("package-2"), _webRoot, null);
            var foreignItemId = (await ctx.CoursePackageItems.AsNoTracking().FirstAsync(i => i.CoursePackageId != id2)).Id;

            var form = (await svc.GetFormAsync(id2))!;
            form.Items[0].Id = foreignItemId;

            var (found, errors) = await svc.UpdateAsync(form, _webRoot, null);

            Assert.True(found);
            Assert.True(errors.ContainsKey("Items"));
        }

        [Fact]
        public async Task Update_Unknown_ReturnsNotFound()
        {
            var (svc, _) = Create();
            var vm = Valid(); vm.Id = 999;
            Assert.False((await svc.UpdateAsync(vm, _webRoot, null)).Found);
        }

        [Fact]
        public async Task Poster_Upload_Replace_Remove_ManageFiles()
        {
            var (svc, ctx) = Create();
            var vm = Valid(); vm.PosterFile = PngFile();
            var (ok, id, errors) = await svc.CreateAsync(vm, _webRoot, null);
            Assert.True(ok, string.Join(",", errors.Values));

            var first = (await ctx.CoursePackages.AsNoTracking().SingleAsync()).PosterImagePath!;
            Assert.StartsWith("/uploads/packages/", first);
            var firstFull = Path.Combine(_webRoot, first.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(firstFull));

            // استبدال: الجديد يُحفظ والقديم يُحذف
            var form = (await svc.GetFormAsync(id))!;
            form.PosterFile = PngFile();
            Assert.Empty((await svc.UpdateAsync(form, _webRoot, null)).Errors);
            var second = (await ctx.CoursePackages.AsNoTracking().SingleAsync()).PosterImagePath!;
            Assert.NotEqual(first, second);
            Assert.False(File.Exists(firstFull));
            var secondFull = Path.Combine(_webRoot, second.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(secondFull));

            // حذف
            form = (await svc.GetFormAsync(id))!;
            form.RemovePoster = true;
            Assert.Empty((await svc.UpdateAsync(form, _webRoot, null)).Errors);
            Assert.Null((await ctx.CoursePackages.AsNoTracking().SingleAsync()).PosterImagePath);
            Assert.False(File.Exists(secondFull));
        }

        [Fact]
        public async Task Poster_CorruptFile_ReturnsFriendlyError_AndSavesNothing()
        {
            var (svc, ctx) = Create();
            var vm = Valid(); vm.PosterFile = FakeFile();

            var (ok, _, errors) = await svc.CreateAsync(vm, _webRoot, null);

            Assert.False(ok);
            Assert.True(errors.ContainsKey("PosterFile"));
            Assert.Equal(0, await ctx.CoursePackages.CountAsync());
        }

        [Fact]
        public async Task Poster_DisallowedExtension_IsRejected()
        {
            var (svc, _) = Create();
            var vm = Valid(); vm.PosterFile = PngFile("poster.svg");
            Assert.True((await svc.CreateAsync(vm, _webRoot, null)).Errors.ContainsKey("PosterFile"));
        }

        [Fact]
        public void DeletePosterFile_OutsidePackagesFolder_IsIgnored()
        {
            var outside = Path.Combine(_webRoot, "secret.txt");
            File.WriteAllText(outside, "x");
            var other = Path.Combine(_webRoot, "uploads", "other");
            Directory.CreateDirectory(other);
            var otherFile = Path.Combine(other, "a.webp");
            File.WriteAllText(otherFile, "x");

            CoursePackageAdminService.DeletePosterFile("/uploads/packages/../../secret.txt", _webRoot);
            CoursePackageAdminService.DeletePosterFile("/secret.txt", _webRoot);
            CoursePackageAdminService.DeletePosterFile("/uploads/other/a.webp", _webRoot);
            CoursePackageAdminService.DeletePosterFile("/uploads/packages/..\\..\\secret.txt", _webRoot);

            Assert.True(File.Exists(outside));
            Assert.True(File.Exists(otherFile));
        }

        [Fact]
        public async Task Delete_WithRegistrations_IsBlocked()
        {
            var (svc, ctx) = Create();
            var (_, id, _) = await svc.CreateAsync(Valid(), _webRoot, null);
            ctx.CoursePackageRegistrations.Add(new CoursePackageRegistration
            {
                CoursePackageId = id, PackageNameSnapshot = "x", FullName = "اسم", PhoneNumber = "0500000000",
                Status = CoursePackageRegistrationStatus.New
            });
            await ctx.SaveChangesAsync();

            Assert.Equal(DeletePackageResult.HasRegistrations, await svc.DeleteAsync(id, _webRoot));
            Assert.Equal(1, await ctx.CoursePackages.CountAsync());
        }

        [Fact]
        public async Task Delete_WithoutRegistrations_RemovesPackageAndPoster()
        {
            var (svc, ctx) = Create();
            var vm = Valid(); vm.PosterFile = PngFile();
            var (_, id, _) = await svc.CreateAsync(vm, _webRoot, null);
            var poster = (await ctx.CoursePackages.AsNoTracking().SingleAsync()).PosterImagePath!;
            var full = Path.Combine(_webRoot, poster.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            Assert.Equal(DeletePackageResult.Deleted, await svc.DeleteAsync(id, _webRoot));

            Assert.Equal(0, await ctx.CoursePackages.CountAsync());
            Assert.Equal(0, await ctx.CoursePackageItems.CountAsync());
            Assert.False(File.Exists(full));
            Assert.Equal(DeletePackageResult.NotFound, await svc.DeleteAsync(id, _webRoot));
        }

        [Fact]
        public async Task Toggles_FlipFlagsAndReportMissing()
        {
            var (svc, ctx) = Create();
            var (_, id, _) = await svc.CreateAsync(Valid(), _webRoot, null);

            Assert.True(await svc.ToggleActiveAsync(id));
            Assert.True(await svc.ToggleHomeVisibilityAsync(id));
            var p = await ctx.CoursePackages.AsNoTracking().SingleAsync();
            Assert.False(p.IsActive);
            Assert.False(p.ShowOnHomePage);

            Assert.False(await svc.ToggleActiveAsync(999));
            Assert.False(await svc.ToggleHomeVisibilityAsync(999));
        }

        [Fact]
        public async Task GetList_ReturnsCounts()
        {
            var (svc, ctx) = Create();
            var (_, id, _) = await svc.CreateAsync(Valid(), _webRoot, null);
            ctx.CoursePackageRegistrations.AddRange(
                new CoursePackageRegistration { CoursePackageId = id, PackageNameSnapshot = "x", FullName = "a", PhoneNumber = "1", Status = CoursePackageRegistrationStatus.New },
                new CoursePackageRegistration { CoursePackageId = id, PackageNameSnapshot = "x", FullName = "b", PhoneNumber = "2", Status = CoursePackageRegistrationStatus.Contacted });
            await ctx.SaveChangesAsync();

            var row = Assert.Single(await svc.GetListAsync());
            Assert.Equal(3, row.ItemsCount);
            Assert.Equal(2, row.RegistrationsCount);
            Assert.Equal(1, row.NewRegistrationsCount);
        }
    }
}
