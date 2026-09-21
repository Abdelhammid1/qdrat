using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;

namespace QdratNew.Services
{
    public class UserContextService : IUserContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserContextService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private HttpContext HttpContext => _httpContextAccessor.HttpContext;
        private ISession Session => HttpContext.Session;
        private ClaimsPrincipal User => HttpContext.User;

        // ✅ الدور النشط من Session (مع fallback ذكي)
        public string ActiveRole
        {
            get
            {
                var role = Session.GetString("ActiveRole");
                if (!string.IsNullOrEmpty(role))
                    return role;

                // fallback من Claims
                var claimRole = User?.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .FirstOrDefault();

                return claimRole;
            }
        }

        // ✅ كل الأدوار دائمًا من Claims (مش Session)
        public List<string> AvailableRoles =>
            User?.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct()
                .ToList()
            ?? new List<string>();

        public bool HasMultipleRoles => AvailableRoles.Count > 1;
    }
}
