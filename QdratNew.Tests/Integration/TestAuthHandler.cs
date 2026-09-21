using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QdratNew.Tests.Integration
{
    // Sprint 9 (QG-G / G2) — مخطط مصادقة وهمي لاختبارات التكامل: يقرأ هوية الطلب
    // من رأسين (X-Test-UserId / X-Test-Role) بدل تسجيل دخول حقيقي عبر Identity،
    // لعزل اختبار التفويض/الحماية عن آلية تسجيل الدخول الفعلية.
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestScheme";
        public const string UserIdHeader = "X-Test-UserId";
        public const string RoleHeader = "X-Test-Role";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserIdHeader, out var userId) || string.IsNullOrWhiteSpace(userId))
                return Task.FromResult(AuthenticateResult.NoResult());

            var role = Request.Headers.TryGetValue(RoleHeader, out var r) ? r.ToString() : "Student";

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
