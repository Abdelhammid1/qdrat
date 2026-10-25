using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // PKG-S6.2 — حماية مسارات أدمن الباقات + رفض POST بلا Anti-forgery (الاختبارات لا تصل إلى جداول الباقات)
    [Collection("SharedIntegrationDb")]
    public class CoursePackagesSecurityTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly CustomWebApplicationFactory _factory;

        public CoursePackagesSecurityTests(CustomWebApplicationFactory factory) => _factory = factory;

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

        private static bool IsDenied(HttpStatusCode s) =>
            s is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found;

        [Theory]
        [InlineData("/Admin/CoursePackages")]
        [InlineData("/Admin/CoursePackages/Create")]
        [InlineData("/Admin/CoursePackageRegistrations")]
        [InlineData("/Admin/CoursePackageRegistrations/Details/1")]
        public async Task AdminPages_Anonymous_AreRejected(string url)
        {
            var response = await CreateClient().GetAsync(url);

            Assert.True(IsDenied(response.StatusCode), $"{url} => {response.StatusCode}");
        }

        [Theory]
        [InlineData("/Admin/CoursePackages")]
        [InlineData("/Admin/CoursePackageRegistrations")]
        public async Task AdminPages_StudentRole_IsNotAllowed(string url)
        {
            var response = await CreateClient("pkg-student-1", "Student").GetAsync(url);

            Assert.True(IsDenied(response.StatusCode), $"{url} => {response.StatusCode}");
        }

        [Theory]
        [InlineData("/Admin/CoursePackages/Create")]
        [InlineData("/Admin/CoursePackages/ToggleActive")]
        [InlineData("/Admin/CoursePackages/Delete")]
        [InlineData("/Admin/CoursePackageRegistrations/UpdateStatus")]
        public async Task AdminPost_WithoutAntiForgeryToken_ReturnsBadRequest(string url)
        {
            var client = CreateClient("pkg-owner-1", "Owner");
            var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = "1" });

            var response = await client.PostAsync(url, form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task PublicSubmit_WithoutAntiForgeryToken_ReturnsBadRequest()
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"] = "أحمد علي",
                ["PhoneNumber"] = "0500000000"
            });

            var response = await CreateClient().PostAsync("/packages/package-1", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
