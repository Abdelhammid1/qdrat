using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Frontend.PublicRegistration;
using QdratNew.ViewModels.Frontend.Register;
using Xunit;

namespace QdratNew.Tests.PublicRegistration
{
    // RL-S4 — كتالوج صفحة التسجيل + إرسال الطلب
    public class PublicRegistrationServiceTests
    {
        private static (PublicRegistrationService svc, ApplicationDbContext ctx, IMemoryCache cache) Create()
        {
            var ctx = TestDbContextFactory.CreateFreshContext(out _);
            var cache = new MemoryCache(new MemoryCacheOptions());
            return (new PublicRegistrationService(ctx, cache), ctx, cache);
        }

        private static async Task<(int visibleCourse, int hiddenCourse)> SeedAsync(ApplicationDbContext ctx)
        {
            var shown = new Project { Name = "القدرات", Description = "d", BranchId = 1, IsActive = true, ShowOnRegisterPage = true, RegisterDisplayOrder = 1 };
            var hiddenProject = new Project { Name = "مخفي", Description = "d", BranchId = 1, IsActive = true, ShowOnRegisterPage = false };
            var emptyProject = new Project { Name = "بلا دورات", Description = "d", BranchId = 1, IsActive = true, ShowOnRegisterPage = true };
            ctx.Projects.AddRange(shown, hiddenProject, emptyProject);
            await ctx.SaveChangesAsync();

            var ok = new Course { Name = "أ", Description = "d", IsActive = true, ShowOnRegisterPage = true, ProjectId = shown.Id, RegisterDisplayOrder = 2 };
            var first = new Course { Name = "ب", Description = "d", IsActive = true, ShowOnRegisterPage = true, ProjectId = shown.Id, RegisterDisplayOrder = 1 };
            var hiddenCourse = new Course { Name = "دورة مخفية", Description = "d", IsActive = true, ShowOnRegisterPage = false, ProjectId = shown.Id };
            var inactive = new Course { Name = "غير نشطة", Description = "d", IsActive = false, ShowOnRegisterPage = true, ProjectId = shown.Id };
            var underHidden = new Course { Name = "تحت برنامج مخفي", Description = "d", IsActive = true, ShowOnRegisterPage = true, ProjectId = hiddenProject.Id };
            ctx.Courses.AddRange(ok, first, hiddenCourse, inactive, underHidden);
            await ctx.SaveChangesAsync();
            return (ok.Id, hiddenCourse.Id);
        }

        private static RegisterLeadFormVM Form(params int[] ids) => new()
        {
            StudentName = "طالب تجريبي",
            PhoneNumber = "0501234567",
            SelectedCourseIds = ids.ToList()
        };

        [Fact]
        public async Task Catalog_ExcludesHiddenInactiveAndEmptyPrograms_AndOrders()
        {
            var (svc, ctx, _) = Create();
            await SeedAsync(ctx);

            var catalog = await svc.GetCatalogAsync();

            var program = Assert.Single(catalog);
            Assert.Equal("القدرات", program.Name);
            Assert.Equal(new[] { "ب", "أ" }, program.Courses.Select(c => c.Name).ToArray());
        }

        [Fact]
        public async Task Catalog_IsCached_AndInvalidateClearsIt()
        {
            var (svc, ctx, cache) = Create();
            await SeedAsync(ctx);
            await svc.GetCatalogAsync();
            Assert.True(cache.TryGetValue(RegisterCatalogCache.Key, out _));

            svc.InvalidateCatalog();
            Assert.False(cache.TryGetValue(RegisterCatalogCache.Key, out _));
        }

        [Fact]
        public async Task Submit_RejectsHiddenOrUnknownCourse()
        {
            var (svc, ctx, _) = Create();
            var (_, hidden) = await SeedAsync(ctx);

            var r1 = await svc.SubmitLeadAsync(Form(hidden), null);
            var r2 = await svc.SubmitLeadAsync(Form(99999), null);

            Assert.False(r1.Success);
            Assert.True(r1.Errors.ContainsKey("SelectedCourseIds"));
            Assert.False(r2.Success);
            Assert.Empty(ctx.FrontendLeads);
        }

        [Fact]
        public async Task Submit_SavesSnapshots_NormalizesArabicDigits_AndSummary()
        {
            var (svc, ctx, _) = Create();
            var (courseId, _) = await SeedAsync(ctx);
            var form = Form(courseId, courseId);
            form.PhoneNumber = "٠٥٠ ١٢٣ ٤٥٦٧";

            var result = await svc.SubmitLeadAsync(form, "1.2.3.4");

            Assert.True(result.Success);
            var lead = await ctx.FrontendLeads.Include(l => l.SelectedCourses).SingleAsync();
            Assert.Equal("0501234567", lead.PhoneNumber);
            Assert.Equal("القدرات (1)", lead.SelectedProgram);
            var line = Assert.Single(lead.SelectedCourses);
            Assert.Equal("أ", line.CourseNameSnapshot);
            Assert.Equal("القدرات", line.ProjectNameSnapshot);
            Assert.Equal(FrontendLeadStatus.New, lead.Status);
        }

        [Fact]
        public async Task Submit_SamePhoneWithin10Minutes_DoesNotDuplicate()
        {
            var (svc, ctx, _) = Create();
            var (courseId, _) = await SeedAsync(ctx);

            await svc.SubmitLeadAsync(Form(courseId), null);
            var second = await svc.SubmitLeadAsync(Form(courseId), null);

            Assert.True(second.Success);
            Assert.Equal(1, await ctx.FrontendLeads.CountAsync());
        }

        [Fact]
        public async Task Submit_Parent_RequiresParentData()
        {
            var (svc, ctx, _) = Create();
            var (courseId, _) = await SeedAsync(ctx);
            var form = Form(courseId);
            form.ApplicantType = LeadApplicantType.Parent;

            var result = await svc.SubmitLeadAsync(form, null);

            Assert.False(result.Success);
            Assert.True(result.Errors.ContainsKey("ParentName"));
            Assert.True(result.Errors.ContainsKey("ParentPhone"));
        }

        [Fact]
        public async Task Submit_MoreThan10Courses_Rejected()
        {
            var (svc, ctx, _) = Create();
            await SeedAsync(ctx);
            var result = await svc.SubmitLeadAsync(Form(Enumerable.Range(1, 11).ToArray()), null);
            Assert.False(result.Success);
            Assert.True(result.Errors.ContainsKey("SelectedCourseIds"));
        }

        [Fact]
        public async Task Submit_EmptyCatalog_RequiresNotes()
        {
            var (svc, _, _) = Create();
            var noNotes = await svc.SubmitLeadAsync(Form(), null);
            Assert.False(noNotes.Success);
            Assert.True(noNotes.Errors.ContainsKey("Notes"));

            var form = Form();
            form.Notes = "أريد القدرات";
            Assert.True((await svc.SubmitLeadAsync(form, null)).Success);
        }
    }
}
