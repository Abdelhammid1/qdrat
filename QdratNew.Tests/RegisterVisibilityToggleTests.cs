using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Areas.Admin.Controllers;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Frontend.PublicRegistration;
using Xunit;

namespace QdratNew.Tests
{
    // RL-S3 — التبديل السريع لظهور البرنامج/الدورة في صفحة التسجيل + إبطال كاش الكتالوج
    public class RegisterVisibilityToggleTests
    {
        private static (ApplicationDbContext ctx, IMemoryCache cache) Create()
            => (TestDbContextFactory.CreateFreshContext(out _), new MemoryCache(new MemoryCacheOptions()));

        [Fact]
        public async Task Project_Toggle_FlipsFlag_AndInvalidatesCache()
        {
            var (ctx, cache) = Create();
            var project = new Project { Name = "P", Description = "D", BranchId = 1 };
            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();
            cache.Set(RegisterCatalogCache.Key, new object());

            var controller = new ProjectsController(ctx, cache);
            var result = await controller.ToggleRegisterVisibility(project.Id);

            Assert.IsType<JsonResult>(result);
            Assert.True((await ctx.Projects.FindAsync(project.Id))!.ShowOnRegisterPage);
            Assert.False(cache.TryGetValue(RegisterCatalogCache.Key, out _));

            await controller.ToggleRegisterVisibility(project.Id);
            Assert.False((await ctx.Projects.FindAsync(project.Id))!.ShowOnRegisterPage);
        }

        [Fact]
        public async Task Project_Toggle_UnknownId_ReturnsNotFound()
        {
            var (ctx, cache) = Create();
            var result = await new ProjectsController(ctx, cache).ToggleRegisterVisibility(999);
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task Course_Toggle_FlipsFlag_AndInvalidatesCache()
        {
            var (ctx, cache) = Create();
            var course = new Course { Name = "C", Description = "D" };
            ctx.Courses.Add(course);
            await ctx.SaveChangesAsync();
            cache.Set(RegisterCatalogCache.Key, new object());

            var result = await new CoursesController(ctx, cache).ToggleRegisterVisibility(course.Id);

            Assert.IsType<JsonResult>(result);
            Assert.True((await ctx.Courses.FindAsync(course.Id))!.ShowOnRegisterPage);
            Assert.False(cache.TryGetValue(RegisterCatalogCache.Key, out _));
        }
    }
}
