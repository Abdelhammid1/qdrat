using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Security.Permissions;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>
    /// الباقات والتواصل خاضعة لنظام البروفايل: الكتالوج (مصفوفة البروفايل) + تسجيل السياسات + حماية كل Action.
    /// </summary>
    public class CommunicationPackagesPermissionTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QdratNew.csproj"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("QdratNew.csproj غير موجود.");
        }

        [Theory]
        [InlineData("CoursePackages")]
        [InlineData("CoursePackageRegistrations")]
        [InlineData("FrontendLeads")]
        [InlineData("ContactMessages")]
        [InlineData("CorporateRequests")]
        public void Catalog_HasReadAction_AndIsListedInProfileMatrix(string controller)
        {
            Assert.Contains(AdminControllerActionCatalog.GetActions(controller), a => a.Key == "Read");
            var src = File.ReadAllText(Path.Combine(RepoRoot(), "Areas", "Admin", "Controllers", "AdminPermissionProfilesController.cs"));
            Assert.Contains($"(\"{controller}\"", src);
        }

        [Fact]
        public void EveryPolicyConstant_IsRegisteredInProgram()
        {
            var program = File.ReadAllText(Path.Combine(RepoRoot(), "Program.cs"));
            var names = typeof(AdminPermissionPolicies).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.Name.StartsWith("CoursePackages_") || f.Name.StartsWith("CoursePackageRegistrations_")
                         || f.Name.StartsWith("FrontendLeads_") || f.Name.StartsWith("ContactMessages_")
                         || f.Name.StartsWith("CorporateRequests_"))
                .Select(f => f.Name).ToList();
            Assert.NotEmpty(names);
            foreach (var n in names) Assert.Contains($"AdminPermissionPolicies.{n}", program);
        }

        [Theory]
        [InlineData(typeof(Areas.Admin.Controllers.CoursePackagesController))]
        [InlineData(typeof(Areas.Admin.Controllers.CoursePackageRegistrationsController))]
        [InlineData(typeof(Areas.Admin.Controllers.FrontendLeadsController))]
        [InlineData(typeof(Areas.Admin.Controllers.ContactMessagesController))]
        [InlineData(typeof(Areas.Admin.Controllers.CorporateRequestsController))]
        public void EveryAction_HasAdminPermission_MatchingItsController(Type controller)
        {
            var name = controller.Name.Replace("Controller", "");
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => typeof(IActionResult).IsAssignableFrom(m.ReturnType)
                         || (m.ReturnType.IsGenericType && typeof(IActionResult).IsAssignableFrom(m.ReturnType.GetGenericArguments()[0])));
            foreach (var m in actions)
            {
                var attr = Assert.Single(m.GetCustomAttributes<AdminPermissionAttribute>());
                Assert.StartsWith(name + ":", attr.Policy);
            }
        }
    }
}
