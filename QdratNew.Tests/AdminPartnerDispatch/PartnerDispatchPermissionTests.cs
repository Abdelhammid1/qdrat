using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Areas.Admin.Controllers;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Implementations;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Security.Permissions;
using Xunit;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S1.3 — وحدة PartnerDispatch في نظام البروفايل + علَم عقد معمل القياس
    public class PartnerDispatchPermissionTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QdratNew.csproj"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("QdratNew.csproj غير موجود.");
        }

        [Fact]
        public void Catalog_HasSendOverrideReadLog_AndIsListedInProfileMatrix()
        {
            var keys = AdminControllerActionCatalog.GetActions("PartnerDispatch").Select(a => a.Key).ToList();
            Assert.Equal(new[] { "Send", "Override", "ReadLog" }, keys);

            var src = File.ReadAllText(Path.Combine(RepoRoot(), "Areas", "Admin", "Controllers", "AdminPermissionProfilesController.cs"));
            Assert.Contains("(\"PartnerDispatch\"", src);
        }

        [Fact]
        public void EveryPolicyConstant_MatchesCatalog_AndIsRegisteredInProgram()
        {
            var program = File.ReadAllText(Path.Combine(RepoRoot(), "Program.cs"));
            var fields = typeof(AdminPermissionPolicies).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.Name.StartsWith("PartnerDispatch_"))
                .ToList();

            Assert.Equal(3, fields.Count);
            var catalog = AdminControllerActionCatalog.GetActions("PartnerDispatch").Select(a => "PartnerDispatch:" + a.Key).ToHashSet();
            foreach (var field in fields)
            {
                Assert.Contains($"AdminPermissionPolicies.{field.Name}", program);
                Assert.Contains((string)field.GetValue(null)!, catalog);
            }
        }

        [Fact]
        public void PartnerDispatchController_EveryAction_RequiresPartnerDispatchPermission()
        {
            var actions = typeof(PartnerDispatchController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => typeof(IActionResult).IsAssignableFrom(m.ReturnType)
                         || (m.ReturnType.IsGenericType && typeof(IActionResult).IsAssignableFrom(m.ReturnType.GetGenericArguments()[0])))
                .ToList();

            Assert.NotEmpty(actions);
            foreach (var m in actions)
            {
                var attr = Assert.Single(m.GetCustomAttributes<AdminPermissionAttribute>());
                Assert.StartsWith("PartnerDispatch:", attr.Policy);
            }
        }

        [Fact]
        public void FeatureType_MinistrySim_IsEleven_AndDistinct()
        {
            Assert.Equal(11, (int)PartnerFeatureType.MinistrySimExams);
            var values = Enum.GetValues<PartnerFeatureType>().Select(v => (int)v).ToList();
            Assert.Equal(values.Count, values.Distinct().Count());
        }

        [Fact]
        public void DispatchEnums_KeepStoredValues()
        {
            Assert.Equal(1, (int)DispatchAudience.Platform);
            Assert.Equal(2, (int)DispatchAudience.Partners);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Enum.GetValues<PartnerDispatchKind>().Select(v => (int)v));
        }

        // =====================================================
        // علَم العقد في خدمة الاشتراك
        // =====================================================
        private static PartnerSubscriptionService ServiceWith(bool flag, bool active, DateTime? accessUntil = null)
        {
            var db = TestDbContextFactory.CreateFreshContext(out _);
            var today = DateTime.Today;
            db.PartnerSubscriptions.Add(new PartnerSubscription
            {
                Id = 1,
                PartnerId = 7,
                StartDate = today.AddDays(-30),
                EndDate = active ? today.AddDays(30) : today.AddDays(-1),
                AccessUntilDate = accessUntil,
                CanUseMinistrySimExams = flag
            });
            db.SaveChanges();
            return new PartnerSubscriptionService(db);
        }

        [Fact]
        public void MinistrySimFlag_RequiresActiveContract()
        {
            Assert.True(ServiceWith(flag: true, active: true).CanUseMinistrySimExams(7));
            Assert.False(ServiceWith(flag: false, active: true).CanUseMinistrySimExams(7));
            Assert.False(ServiceWith(flag: true, active: false).CanUseMinistrySimExams(7));
        }

        [Fact]
        public void MinistrySimFlag_IsReadableAfterExpiry_OnlyWhenTheContractIncludedIt()
        {
            var until = DateTime.Today.AddDays(10);
            Assert.True(ServiceWith(flag: true, active: false, accessUntil: until).CanReadFeatureAfterExpiry(7, PartnerFeatureType.MinistrySimExams));
            Assert.False(ServiceWith(flag: false, active: false, accessUntil: until).CanReadFeatureAfterExpiry(7, PartnerFeatureType.MinistrySimExams));
        }
    }
}
