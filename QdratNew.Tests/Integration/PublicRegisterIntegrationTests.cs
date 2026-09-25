using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Entities.Frontend;
using QdratNew.Enums;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // RL-S6.2 — /register (GET/POST/429) + لوحة الأدمن (حماية + UpdateStatus) على التطبيق الفعلي.
    // القاعدة: نسخة QdratNewDB_IntegrationTests المعزولة. كل صف نزرعه يحمل بادئة "RL-IT" ويُحذف قبل/بعد كل اختبار.
    public abstract class RegisterIntegrationBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        protected const string Tag = "RL-IT";
        protected readonly CustomWebApplicationFactory Factory;
        protected int CourseId;

        protected RegisterIntegrationBase(CustomWebApplicationFactory factory) => Factory = factory;

        public async Task InitializeAsync()
        {
            await TestDatabaseBaseline.EnsureAsync(); // لا نستدعي ResetDatabaseAsync: يحذف بيانات اختبارات أخرى تعمل بالتوازي
            await CleanAsync();
            await SeedCatalogAsync();
        }

        public Task DisposeAsync() => CleanAsync();

        protected HttpClient CreateClient(string? userId = null, string? role = null)
        {
            var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            if (userId != null)
            {
                client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
                client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role ?? "Student");
            }
            return client;
        }

        private async Task CleanAsync()
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM FrontendLeadCourses WHERE CourseNameSnapshot LIKE N'RL-IT%' OR FrontendLeadId IN (SELECT Id FROM FrontendLeads WHERE StudentName LIKE N'RL-IT%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM FrontendLeads WHERE StudentName LIKE N'RL-IT%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Courses WHERE Name LIKE N'RL-IT%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Projects WHERE Name LIKE N'RL-IT%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Branches WHERE Name LIKE N'RL-IT%'");
        }

        private async Task SeedCatalogAsync()
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var branch = new Branch { Name = $"{Tag} فرع", Location = "-", City = "-", State = "-", Country = "-" };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();

            var project = new Project { Name = $"{Tag} برنامج", Description = "-", BranchId = branch.Id, IsActive = true, ShowOnRegisterPage = true };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var course = new Course { Name = $"{Tag} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true, ShowOnRegisterPage = true, ProjectId = project.Id };
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            CourseId = course.Id;

            // الكاش يحمل كتالوجًا قديمًا من أي طلب سابق
            Factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()
                .Remove(QdratNew.Services.Frontend.PublicRegistration.RegisterCatalogCache.Key);
        }

        // GET /register ← (كوكي antiforgery + توكن الفورم)
        protected static async Task<(string Cookie, string Token)> GetRegisterTokensAsync(HttpClient client)
        {
            var response = await client.GetAsync("/register");
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();

            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            var cookie = response.Headers.GetValues("Set-Cookie")
                .Select(c => c.Split(';')[0])
                .First(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal));
            Assert.False(string.IsNullOrEmpty(token), "لم يُعثر على توكن antiforgery في صفحة /register");
            return (cookie, token);
        }

        protected FormUrlEncodedContent ValidForm(string token, string phone = "0501234567") => new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Form.StudentName"] = $"{Tag} طالب",
            ["Form.PhoneNumber"] = phone,
            ["Form.ApplicantType"] = "1",
            ["Form.SelectedCourseIds"] = CourseId.ToString()
        });
    }

    [Collection("SharedIntegrationDb")]
    public class PublicRegisterIntegrationTests : RegisterIntegrationBase
    {
        public PublicRegisterIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

        [Fact]
        public async Task Get_Register_Returns200_AndShowsVisibleCourse()
        {
            var response = await CreateClient().GetAsync("/register");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains($"{Tag} دورة", html);
        }

        [Fact]
        public async Task Post_Valid_RedirectsToSuccess_AndPersistsLeadWithSnapshot()
        {
            var client = CreateClient();
            var (cookie, token) = await GetRegisterTokensAsync(client);
            client.DefaultRequestHeaders.Add("Cookie", cookie);

            var response = await client.PostAsync("/register", ValidForm(token));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.EndsWith("/register/success", response.Headers.Location!.ToString());

            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lead = await db.FrontendLeads.Include(l => l.SelectedCourses).SingleAsync(l => l.StudentName == $"{Tag} طالب");
            Assert.Equal(FrontendLeadStatus.New, lead.Status);
            Assert.Equal($"{Tag} دورة", Assert.Single(lead.SelectedCourses).CourseNameSnapshot);
        }

        [Fact]
        public async Task Post_WithoutAntiForgeryToken_ReturnsBadRequest_AndSavesNothing()
        {
            var client = CreateClient();
            var form = ValidForm("missing");

            var response = await client.PostAsync("/register", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.FrontendLeads.AnyAsync(l => l.StudentName == $"{Tag} طالب"));
        }

        [Fact]
        public async Task Post_HoneypotFilled_RedirectsButSavesNothing()
        {
            var client = CreateClient();
            var (cookie, token) = await GetRegisterTokensAsync(client);
            client.DefaultRequestHeaders.Add("Cookie", cookie);
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Form.StudentName"] = $"{Tag} بوت",
                ["Form.PhoneNumber"] = "0501234567",
                ["Form.Website"] = "http://spam.example"
            });

            var response = await client.PostAsync("/register", form);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.FrontendLeads.AnyAsync(l => l.StudentName == $"{Tag} بوت"));
        }
    }

    // صنف مستقل: لكل صنف Factory خاص به، فعدّاد الـ Rate limit لا يتأثر ببقية الاختبارات
    [Collection("SharedIntegrationDb")]
    public class PublicRegisterRateLimitTests : RegisterIntegrationBase
    {
        public PublicRegisterRateLimitTests(CustomWebApplicationFactory factory) : base(factory) { }

        [Fact]
        public async Task Post_SixthRequestWithin10Minutes_Returns429()
        {
            var client = CreateClient();
            var (cookie, token) = await GetRegisterTokensAsync(client);
            client.DefaultRequestHeaders.Add("Cookie", cookie);

            for (var i = 1; i <= 5; i++)
            {
                var ok = await client.PostAsync("/register", ValidForm(token));
                Assert.NotEqual(HttpStatusCode.TooManyRequests, ok.StatusCode);
            }

            var sixth = await client.PostAsync("/register", ValidForm(token));

            Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        }
    }

    [Collection("SharedIntegrationDb")]
    public class FrontendLeadsUpdateStatusIntegrationTests : RegisterIntegrationBase
    {
        public FrontendLeadsUpdateStatusIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

        private async Task<int> SeedLeadAsync()
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lead = new FrontendLead
            {
                StudentName = $"{Tag} لوحة",
                PhoneNumber = "0500000000",
                SelectedProgram = "",
                Status = FrontendLeadStatus.New,
                CreatedAt = DateTime.UtcNow
            };
            db.FrontendLeads.Add(lead);
            await db.SaveChangesAsync();
            return lead.Id;
        }

        [Fact]
        public async Task Index_Anonymous_IsRedirectedOrRejected()
        {
            var response = await CreateClient().GetAsync("/Admin/FrontendLeads");

            Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Index_AsOwner_RendersDashboardWithSearchAndLead()
        {
            await SeedLeadAsync();
            var client = CreateClient("rl-owner-1", "Owner");

            var response = await client.GetAsync("/Admin/FrontendLeads");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("id=\"flSearch\"", html);
            Assert.Contains($"{Tag} لوحة", html);
        }

        [Fact]
        public async Task UpdateStatus_AsOwner_WithToken_UpdatesStatusAndStampsContact()
        {
            var id = await SeedLeadAsync();
            var client = CreateClient("rl-owner-1", "Owner");
            var (cookie, token) = Factory.CreateValidAntiForgeryTokens("rl-owner-1");
            client.DefaultRequestHeaders.Add("Cookie", cookie);
            client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
            var body = new StringContent($"{{\"Id\":{id},\"Status\":2,\"AdminNotes\":\"تم الاتصال\"}}", Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/Admin/FrontendLeads/UpdateStatus", body);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lead = await db.FrontendLeads.AsNoTracking().SingleAsync(l => l.Id == id);
            Assert.Equal(FrontendLeadStatus.Contacted, lead.Status);
            Assert.True(lead.IsContacted);
            Assert.NotNull(lead.ContactedAt);
            Assert.Equal("تم الاتصال", lead.AdminNotes);
        }

        [Fact]
        public async Task UpdateStatus_UnknownStatusValue_ReturnsBadRequest()
        {
            var id = await SeedLeadAsync();
            var client = CreateClient("rl-owner-1", "Owner");
            var (cookie, token) = Factory.CreateValidAntiForgeryTokens("rl-owner-1");
            client.DefaultRequestHeaders.Add("Cookie", cookie);
            client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
            var body = new StringContent($"{{\"Id\":{id},\"Status\":99}}", Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/Admin/FrontendLeads/UpdateStatus", body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
