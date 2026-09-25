using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using QdratNew.Enums;
using QdratNew.Services.Frontend.Leads;
using QdratNew.ViewComponents;
using QdratNew.ViewModels.Admin.FrontendLeads;
using Xunit;

namespace QdratNew.Tests
{
    // RL-S5 — إحصائيات اللوحة + تحديث الحالة + الطلبات القديمة
    public class FrontendLeadAdminServiceTests
    {
        private static (FrontendLeadAdminService svc, ApplicationDbContext ctx, IMemoryCache cache) Create()
        {
            var ctx = TestDbContextFactory.CreateFreshContext(out _);
            var cache = new MemoryCache(new MemoryCacheOptions());
            return (new FrontendLeadAdminService(ctx, cache), ctx, cache);
        }

        private static FrontendLead Lead(string name, FrontendLeadStatus status, DateTime createdAt, string program = "")
            => new()
            {
                StudentName = name,
                PhoneNumber = "0500000000",
                Status = status,
                IsContacted = status != FrontendLeadStatus.New,
                CreatedAt = createdAt,
                SelectedProgram = program
            };

        [Fact]
        public async Task GetDashboard_CountsByStatus_AndRecentWindows()
        {
            var (svc, ctx, _) = Create();
            var now = DateTime.UtcNow;
            ctx.FrontendLeads.AddRange(
                Lead("a", FrontendLeadStatus.New, now),
                Lead("b", FrontendLeadStatus.New, now.AddDays(-3)),
                Lead("c", FrontendLeadStatus.Contacted, now.AddDays(-20)),
                Lead("d", FrontendLeadStatus.NeedFollowUp, now.AddDays(-20)),
                Lead("e", FrontendLeadStatus.Rejected, now.AddDays(-20)),
                Lead("f", FrontendLeadStatus.Converted, now.AddDays(-20)));
            await ctx.SaveChangesAsync();

            var vm = await svc.GetDashboardAsync();

            Assert.Equal(6, vm.Counts.TotalCount);
            Assert.Equal(2, vm.Counts.NewCount);
            Assert.Equal(1, vm.Counts.ContactedCount);
            Assert.Equal(1, vm.Counts.NeedFollowUpCount);
            Assert.Equal(1, vm.Counts.RejectedCount);
            Assert.Equal(1, vm.Counts.ConvertedCount);
            Assert.Equal(2, vm.Counts.Last7DaysCount);
            Assert.Equal(6, vm.Leads.Count);
            Assert.Equal("a", vm.Leads[0].StudentName); // الأحدث أولًا
        }

        [Fact]
        public async Task GetDashboard_EmptyDatabase_ReturnsZeros()
        {
            var (svc, _, _) = Create();

            var vm = await svc.GetDashboardAsync();

            Assert.Equal(0, vm.Counts.TotalCount);
            Assert.Empty(vm.Leads);
            Assert.Empty(vm.ProjectCounts);
        }

        [Fact]
        public async Task GetDashboard_MarksLegacyLeads_AndCountsLeadsPerProject()
        {
            var (svc, ctx, _) = Create();
            var legacy = Lead("قديم", FrontendLeadStatus.New, DateTime.UtcNow, "القدرات");
            var modern = Lead("حديث", FrontendLeadStatus.New, DateTime.UtcNow.AddMinutes(-1), "القدرات (2)");
            modern.SelectedCourses = new List<FrontendLeadCourse>
            {
                new() { ProjectId = 7, ProjectNameSnapshot = "القدرات", CourseNameSnapshot = "دورة 1" },
                new() { ProjectId = 7, ProjectNameSnapshot = "القدرات", CourseNameSnapshot = "دورة 2" }
            };
            ctx.FrontendLeads.AddRange(legacy, modern);
            await ctx.SaveChangesAsync();

            var vm = await svc.GetDashboardAsync();

            var legacyRow = vm.Leads.Single(l => l.StudentName == "قديم");
            var modernRow = vm.Leads.Single(l => l.StudentName == "حديث");
            Assert.True(legacyRow.IsLegacy);
            Assert.Equal("القدرات", legacyRow.LegacyProgram);
            Assert.False(modernRow.IsLegacy);
            Assert.Equal(2, modernRow.Courses.Count);

            var project = Assert.Single(vm.ProjectCounts);
            Assert.Equal(7, project.ProjectId);
            Assert.Equal(1, project.Count); // طلب واحد رغم دورتين
        }

        [Fact]
        public async Task UpdateStatus_ToContacted_SetsAuditFields_AndInvalidatesBadge()
        {
            var (svc, ctx, cache) = Create();
            var lead = Lead("a", FrontendLeadStatus.New, DateTime.UtcNow);
            ctx.FrontendLeads.Add(lead);
            await ctx.SaveChangesAsync();
            cache.Set(NewLeadsBadgeViewComponent.CacheKey, 5);

            var result = await svc.UpdateStatusAsync(
                new UpdateLeadStatusRequest { Id = lead.Id, Status = FrontendLeadStatus.Contacted, AdminNotes = "  اتصلت  " }, "admin-1");

            Assert.True(result.Found);
            var saved = await ctx.FrontendLeads.FindAsync(lead.Id);
            Assert.Equal(FrontendLeadStatus.Contacted, saved!.Status);
            Assert.True(saved.IsContacted);
            Assert.NotNull(saved.ContactedAt);
            Assert.Equal("admin-1", saved.ContactedByUserId);
            Assert.Equal("اتصلت", saved.AdminNotes);
            Assert.NotNull(saved.LastUpdatedAt);
            Assert.False(cache.TryGetValue(NewLeadsBadgeViewComponent.CacheKey, out _));
            Assert.Equal(1, result.Counts.ContactedCount);
            Assert.Equal(0, result.Counts.NewCount);
        }

        [Fact]
        public async Task UpdateStatus_BackToNew_KeepsFirstContactAudit_AndClearsIsContacted()
        {
            var (svc, ctx, _) = Create();
            var lead = Lead("a", FrontendLeadStatus.New, DateTime.UtcNow);
            ctx.FrontendLeads.Add(lead);
            await ctx.SaveChangesAsync();

            await svc.UpdateStatusAsync(new UpdateLeadStatusRequest { Id = lead.Id, Status = FrontendLeadStatus.Converted }, "u1");
            var firstContact = (await ctx.FrontendLeads.FindAsync(lead.Id))!.ContactedAt;
            await svc.UpdateStatusAsync(new UpdateLeadStatusRequest { Id = lead.Id, Status = FrontendLeadStatus.New }, "u2");

            var saved = await ctx.FrontendLeads.FindAsync(lead.Id);
            Assert.False(saved!.IsContacted);
            Assert.Equal(firstContact, saved.ContactedAt);
            Assert.Equal("u1", saved.ContactedByUserId);
        }

        [Fact]
        public async Task UpdateStatus_UnknownId_ReturnsNotFound()
        {
            var (svc, _, _) = Create();

            var result = await svc.UpdateStatusAsync(new UpdateLeadStatusRequest { Id = 999, Status = FrontendLeadStatus.Contacted }, "u");

            Assert.False(result.Found);
        }

        [Fact]
        public async Task MarkContacted_PreservesAdminNotes()
        {
            var (svc, ctx, _) = Create();
            var lead = Lead("a", FrontendLeadStatus.New, DateTime.UtcNow);
            lead.AdminNotes = "ملاحظة سابقة";
            ctx.FrontendLeads.Add(lead);
            await ctx.SaveChangesAsync();

            Assert.True(await svc.MarkContactedAsync(lead.Id, "u"));

            var saved = await ctx.FrontendLeads.FindAsync(lead.Id);
            Assert.Equal(FrontendLeadStatus.Contacted, saved!.Status);
            Assert.Equal("ملاحظة سابقة", saved.AdminNotes);
        }
    }
}
