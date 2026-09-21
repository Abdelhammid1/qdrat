using QdratNew.Services;
using System.Security.Claims;

namespace QdratNew.Middleware
{
    /// <summary>
    /// يعمل بعد UseAuthentication وقبل UseAuthorization.
    /// إذا كان هناك cookie تخفٍّ نشط، يستبدل HttpContext.User
    /// بـ principal يحمل claims المستخدم المستهدف،
    /// مع الإبقاء على auth cookie الأدمن الأصلي دون تغيير.
    /// </summary>
    public class ImpersonationMiddleware
    {
        private readonly RequestDelegate _next;

        public ImpersonationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ImpersonationService impSvc)
        {
            // لا نفعل شيئاً إذا لم يكن الأدمن مسجّل دخوله
            if (context.User.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            var data = impSvc.Read();
            if (data == null)
            {
                await _next(context);
                return;
            }

            // بناء هوية المستخدم المستهدف
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, data.TargetUserId),
                new Claim(ClaimTypes.Name,           data.TargetName),
                new Claim("imp_active",              "1"),
                new Claim("imp_admin_id",            data.AdminId),
            };

            foreach (var role in data.TargetRoles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            // استخدم نفس نوع المصادقة المستخدم حالياً
            var authType = context.User.Identity.AuthenticationType ?? "Cookies";
            var identity = new ClaimsIdentity(claims, authType);
            context.User = new ClaimsPrincipal(identity);

            await _next(context);
        }
    }
}
