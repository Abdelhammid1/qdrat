using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;

namespace QdratNew.Services
{
    public class ImpersonationData
    {
        public string AdminId      { get; set; } = "";
        public string TargetUserId { get; set; } = "";
        public string TargetName   { get; set; } = "";
        public string TargetEmail  { get; set; } = "";
        public List<string> TargetRoles { get; set; } = new();
        public DateTime StartUtc   { get; set; }
    }

    public class ImpersonationService
    {
        private const string CookieName = "ImpersonationState";
        private readonly IDataProtector _protector;
        private readonly IHttpContextAccessor _accessor;

        public ImpersonationService(IDataProtectionProvider dp, IHttpContextAccessor accessor)
        {
            _protector = dp.CreateProtector("QdratNew.Impersonation.v1");
            _accessor  = accessor;
        }

        public void Start(ImpersonationData data)
        {
            var json      = JsonSerializer.Serialize(data);
            var encrypted = _protector.Protect(json);

            _accessor.HttpContext!.Response.Cookies.Append(CookieName, encrypted, new CookieOptions
            {
                HttpOnly  = true,
                SameSite  = SameSiteMode.Lax,
                IsEssential = true,
                Secure    = false,              // true in production
                Path      = "/"
            });
        }

        public ImpersonationData? Read()
        {
            var ctx = _accessor.HttpContext;
            if (ctx == null) return null;

            var cookie = ctx.Request.Cookies[CookieName];
            if (string.IsNullOrEmpty(cookie)) return null;

            try
            {
                var json = _protector.Unprotect(cookie);
                return JsonSerializer.Deserialize<ImpersonationData>(json);
            }
            catch { return null; }
        }

        public void Clear()
        {
            _accessor.HttpContext?.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
        }

        public bool IsActive => Read() != null;
    }
}
