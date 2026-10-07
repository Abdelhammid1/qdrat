using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Security.Permissions;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>
    /// RTK-S10 (D27): صلاحية ManageReview — الكتالوج، تسجيل السياسة، حماية الـ Action، ومعالج التفويض
    /// (استثناء SuperAdmin/Owner/Developer، رفض موظف بلا بروفايل/صلاحية). InMemory للمعالج.
    /// </summary>
    public class RemedialTrackPermissionTests
    {
        private const string Controller = "RemedialTrackPublications";
        private const string Action = "ManageReview";

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QdratNew.csproj"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("QdratNew.csproj غير موجود.");
        }

        // ═══════════ كتالوج وسياسة وAction ═══════════

        [Fact]
        public void Catalog_ListsManageReview_UnderPublications()
            => Assert.Contains(AdminControllerActionCatalog.GetActions(Controller), a => a.Key == Action && !string.IsNullOrWhiteSpace(a.DisplayNameAr));

        [Fact]
        public void PolicyConstant_MatchesControllerAndAction()
            => Assert.Equal($"{Controller}:{Action}", AdminPermissionPolicies.RemedialTrackPublications_ManageReview);

        [Fact]
        public void PolicyConstant_IsRegisteredInProgram()
        {
            var program = File.ReadAllText(Path.Combine(RepoRoot(), "Program.cs"));
            Assert.Contains("AdminPermissionPolicies.RemedialTrackPublications_ManageReview", program);
        }

        [Fact]
        public void SetVideoReview_IsPost_WithAntiForgery_AndManageReviewPolicy()
        {
            var m = typeof(Areas.Admin.Controllers.RemedialTrackPublicationsController).GetMethod("SetVideoReview");
            Assert.NotNull(m);
            Assert.NotNull(m!.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            var auth = Assert.Single(m.GetCustomAttributes<AdminPermissionAttribute>());
            Assert.Equal($"{Controller}:{Action}", auth.Policy);
        }

        [Fact]
        public void ProfileMatrix_ListsTheThreeRemedialSections()
        {
            var m = typeof(Areas.Admin.Controllers.AdminPermissionProfilesController)
                .GetMethod("GetControllersCatalog", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(m);
            var keys = ((List<(string Key, string Name)>)m!.Invoke(null, null)!).Select(x => x.Key).ToList();
            Assert.Contains("RemedialTracks", keys);
            Assert.Contains("RemedialTrackPublications", keys);
            Assert.Contains("RemedialTrackReports", keys);
        }

        // ═══════════ معالج التفويض ═══════════

        private static ClaimsPrincipal User(string userId, params string[] roles)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        private static async Task<bool> AuthorizeAsync(ApplicationDbContext db, ClaimsPrincipal user)
        {
            var req = new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.RemedialTrackPublications_ManageReview);
            var ctx = new AuthorizationHandlerContext(new[] { req }, user, null);
            await new AdminPermissionAuthorizationHandler(db).HandleAsync(ctx);
            return ctx.HasSucceeded;
        }

        private static async Task SeedStaffAsync(ApplicationDbContext db, string userId,
            AdminControllerAccessLevel level, bool custom = false, params (string Action, bool Allowed)[] customActions)
        {
            var profile = new AdminProfile { Name = "موظف " + userId };
            db.AdminProfiles.Add(profile);
            await db.SaveChangesAsync();

            var perm = new AdminProfileControllerPermission
            {
                AdminProfileId = profile.Id, ControllerName = Controller, AccessLevel = level, HasCustomPermissions = custom
            };
            foreach (var (a, allowed) in customActions)
                perm.CustomPermissions.Add(new AdminProfileCustomPermission { ActionName = a, IsAllowed = allowed });
            db.AdminProfileControllerPermissions.Add(perm);
            db.AdminUserProfiles.Add(new AdminUserProfile { UserId = userId, AdminProfileId = profile.Id });
            await db.SaveChangesAsync();
        }

        [Theory]
        [InlineData("SuperAdmin")]
        [InlineData("Owner")]
        [InlineData("Developer")]
        public async Task PrivilegedRoles_Succeed_WithoutAnyProfile(string role)
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            Assert.True(await AuthorizeAsync(db, User("u-priv", role)));
        }

        [Fact]
        public async Task Staff_WithoutProfile_IsDenied()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            Assert.False(await AuthorizeAsync(db, User("u-none", "Admin")));
        }

        [Fact]
        public async Task Anonymous_IsDenied()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            Assert.False(await AuthorizeAsync(db, new ClaimsPrincipal(new ClaimsIdentity())));
        }

        [Fact]
        public async Task Staff_ReadOnlyLevel_IsDenied()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            await SeedStaffAsync(db, "u-read", AdminControllerAccessLevel.Read);
            Assert.False(await AuthorizeAsync(db, User("u-read", "Admin")));
        }

        [Fact]
        public async Task Staff_ReadWriteLevel_WithoutCustom_IsAllowed()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            await SeedStaffAsync(db, "u-rw", AdminControllerAccessLevel.ReadWrite);
            Assert.True(await AuthorizeAsync(db, User("u-rw", "Admin")));
        }

        [Fact]
        public async Task Staff_Custom_OnlyWithManageReviewAllowed_Succeeds()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            await SeedStaffAsync(db, "u-ok", AdminControllerAccessLevel.Read, true, (Action, true), ("Delete", false));
            await SeedStaffAsync(db, "u-no", AdminControllerAccessLevel.ReadWrite, true, ("Delete", true), (Action, false));
            await SeedStaffAsync(db, "u-other", AdminControllerAccessLevel.ReadWrite, true, ("Restore", true));

            Assert.True(await AuthorizeAsync(db, User("u-ok", "Admin")));
            Assert.False(await AuthorizeAsync(db, User("u-no", "Admin")));       // مسموح له Delete فقط
            Assert.False(await AuthorizeAsync(db, User("u-other", "Admin")));    // لا ManageReview ضمن المخصّص
        }
    }
}
