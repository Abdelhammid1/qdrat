using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // RL-S1.4 — حماية /Admin/FrontendLeads: كان الـ Controller الوحيد في Area Admin بلا Authorize.
    [Collection("SharedIntegrationDb")]
    public class FrontendLeadsSecurityTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly CustomWebApplicationFactory _factory;

        public FrontendLeadsSecurityTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        public Task InitializeAsync() => _factory.ResetDatabaseAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        private HttpClient CreateClient(string? userId = null, string? role = null)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            if (userId != null)
            {
                client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
                client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role ?? "Student");
            }
            return client;
        }

        [Fact]
        public async Task Index_Anonymous_IsRejected()
        {
            var client = CreateClient();

            var response = await client.GetAsync("/Admin/FrontendLeads");

            Assert.True(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect or HttpStatusCode.Found,
                $"Unexpected status for anonymous request: {response.StatusCode}");
        }

        [Fact]
        public async Task Index_StudentRole_IsNotAllowed()
        {
            var client = CreateClient("rl-student-1", "Student");

            var response = await client.GetAsync("/Admin/FrontendLeads");

            Assert.True(
                response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized or HttpStatusCode.Redirect or HttpStatusCode.Found,
                $"Unexpected status for student request: {response.StatusCode}");
        }

        [Fact]
        public async Task MarkContacted_WithoutAntiForgeryToken_ReturnsBadRequest()
        {
            var client = CreateClient("rl-owner-1", "Owner");
            var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = "1" });

            var response = await client.PostAsync("/Admin/FrontendLeads/MarkContacted", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // RL-S5.3
        [Fact]
        public async Task UpdateStatus_WithoutAntiForgeryToken_ReturnsBadRequest()
        {
            var client = CreateClient("rl-owner-1", "Owner");
            var body = new StringContent("{\"Id\":1,\"Status\":2}", System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/Admin/FrontendLeads/UpdateStatus", body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
