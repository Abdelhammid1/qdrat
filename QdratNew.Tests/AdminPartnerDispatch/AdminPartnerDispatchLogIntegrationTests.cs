using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Areas.Admin.Controllers;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S6.1 — صفحة سجل الإرسال عبر HTTP: الصلاحية ReadLog، العرض والفلترة، التصدير، ورابط السايدبار.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class AdminPartnerDispatchLogIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string OwnerUser = "apds1-owner";
        private const string NoLogUser = "apds1-no-log";
        private const string LogOnlyUser = "apds1-log-only";

        private readonly CustomWebApplicationFactory _factory;
        private ApdSeed _seed = null!;

        public AdminPartnerDispatchLogIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ApdTestData.CleanupAsync(db);
            _seed = await ApdTestData.SeedAsync(db);

            db.Users.AddRange(ApdTestData.NewUser(OwnerUser), ApdTestData.NewUser(NoLogUser), ApdTestData.NewUser(LogOnlyUser));

            var group = Guid.NewGuid();
            var now = DateTime.UtcNow;
            db.PartnerDispatchLogs.AddRange(
                Log(group, _seed.PartnerA, _seed.BatchA1, 3, now, $"{ApdTestData.Marker} واجب الرياضيات"),
                Log(group, _seed.PartnerB, _seed.BatchB1, 2, now, $"{ApdTestData.Marker} واجب الرياضيات", overridden: true),
                Log(Guid.NewGuid(), _seed.PartnerB, _seed.BatchB1, 5, now.AddMinutes(-5), $"{ApdTestData.Marker} اختبار خاص بـ B", PartnerDispatchKind.Exam));
            await db.SaveChangesAsync();

            // بروفايل بلا الوحدة، وبروفايل فيه ReadLog فقط (بلا Send)
            var noLog = new AdminProfile { Name = $"{ApdTestData.Marker} {NoLogUser}" };
            var logOnly = new AdminProfile { Name = $"{ApdTestData.Marker} {LogOnlyUser}" };
            db.AdminProfiles.AddRange(noLog, logOnly);
            await db.SaveChangesAsync();

            db.AdminProfileControllerPermissions.Add(new AdminProfileControllerPermission
            {
                AdminProfileId = noLog.Id,
                ControllerName = "PerformanceIndicatorExams",
                AccessLevel = AdminControllerAccessLevel.Read
            });
            var dispatchModule = new AdminProfileControllerPermission
            {
                AdminProfileId = logOnly.Id,
                ControllerName = "PartnerDispatch",
                AccessLevel = AdminControllerAccessLevel.Read,
                HasCustomPermissions = true
            };
            dispatchModule.CustomPermissions.Add(new AdminProfileCustomPermission { ActionName = "ReadLog", IsAllowed = true });
            db.AdminProfileControllerPermissions.Add(dispatchModule);
            db.AdminUserProfiles.AddRange(
                new AdminUserProfile { UserId = NoLogUser, AdminProfileId = noLog.Id },
                new AdminUserProfile { UserId = LogOnlyUser, AdminProfileId = logOnly.Id });
            await db.SaveChangesAsync();
        }

        public async Task DisposeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            await ApdTestData.CleanupAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private static PartnerDispatchLog Log(Guid group, int partnerId, int batchId, int students, DateTime at, string title,
            PartnerDispatchKind kind = PartnerDispatchKind.Homework, bool overridden = false) => new()
        {
            DispatchGroupId = group,
            Kind = kind,
            Title = title,
            PartnerId = partnerId,
            BatchId = batchId,
            TargetEntityId = 1,
            StudentsCount = students,
            ContractOverridden = overridden,
            OverrideReason = overridden ? "سبب تجاوز للاختبار" : null,
            SentByUserId = ApdTestData.SenderId,
            SentAtUtc = at
        };

        private HttpClient Client(string userId, string role)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
            return client;
        }

        private static async Task<string> HtmlAsync(HttpResponseMessage response)
            => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        [Fact]
        public async Task Owner_SeesGroupedLog_WithOverrideReason_AndNoStudentResultLinks()
        {
            var response = await Client(OwnerUser, "Owner").GetAsync("/Admin/PartnerDispatchLog");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = await HtmlAsync(response);
            Assert.Contains("سجل الإرسال للمدارس الشريكة", html);
            Assert.Contains($"{ApdTestData.Marker} واجب الرياضيات", html);
            Assert.Contains("2 مدرسة · 2 دفعة · 5 طالب", html);
            Assert.Contains("سبب تجاوز للاختبار", html);
            Assert.DoesNotContain("StudentResult", html);
            Assert.DoesNotContain("ExamStudents", html);
        }

        [Fact]
        public async Task PartnerFilter_ShowsOnlyThatSchool()
        {
            var html = await HtmlAsync(await Client(OwnerUser, "Owner").GetAsync($"/Admin/PartnerDispatchLog?PartnerId={_seed.PartnerA}"));

            Assert.Contains($"{ApdTestData.Marker} واجب الرياضيات", html);
            Assert.Contains("1 مدرسة · 1 دفعة · 3 طالب", html);
            Assert.DoesNotContain("اختبار خاص بـ B", html);
        }

        [Fact]
        public async Task Export_ReturnsXlsx_ForReadLog()
        {
            var response = await Client(LogOnlyUser, "Admin").GetAsync($"/Admin/PartnerDispatchLog/Export?PartnerId={_seed.PartnerB}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
            Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 0);
        }

        [Fact]
        public async Task ReadLogOnly_CanOpenLog_AndSeesSidebarLink()
        {
            var response = await Client(LogOnlyUser, "Admin").GetAsync("/Admin/PartnerDispatchLog");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("/Admin/PartnerDispatchLog", await HtmlAsync(response));
        }

        [Theory]
        [InlineData("/Admin/PartnerDispatchLog")]
        [InlineData("/Admin/PartnerDispatchLog/Export")]
        public async Task WithoutReadLog_IsForbidden(string url)
        {
            var response = await Client(NoLogUser, "Admin").GetAsync(url);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Partner_CannotOpenAdminLog()
        {
            var response = await Client(OwnerUser, "Partner").GetAsync("/Admin/PartnerDispatchLog");
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void EveryAction_RequiresReadLog()
        {
            var actions = typeof(PartnerDispatchLogController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => typeof(IActionResult).IsAssignableFrom(m.ReturnType)
                         || (m.ReturnType.IsGenericType && typeof(IActionResult).IsAssignableFrom(m.ReturnType.GetGenericArguments()[0])))
                .ToList();

            Assert.Equal(2, actions.Count);
            Assert.All(actions, m => Assert.Equal("PartnerDispatch:ReadLog", Assert.Single(m.GetCustomAttributes<AdminPermissionAttribute>()).Policy));
        }
    }
}
