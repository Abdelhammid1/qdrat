using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Middleware
{
    /// <summary>
    /// يتحقق في كل request من أن المستخدم المسجّل دخوله لا يزال IsActive=true.
    /// إذا أوقفه الأدمن أثناء الجلسة، يتم تسجيل خروجه فوراً وإعادته لصفحة الدخول.
    /// يُستثنى: طلبات التخفي (imp_active)، الملفات الساكنة، صفحة الدخول والخروج.
    /// </summary>
    public class ActiveUserMiddleware
    {
        private readonly RequestDelegate _next;

        private static readonly HashSet<string> _skipPrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "/lms/login",
            "/identity/account/logout",
            "/identity/account/accessdenied",
            "/css/", "/js/", "/lib/", "/images/", "/uploads/", "/fonts/",
        };

        public ActiveUserMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext dbContext)
        {
            // تخطي إذا لم يكن مسجّلاً
            if (context.User.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            // تخطي طلبات التخفي — الأدمن هو المسجّل فعلياً
            if (context.User.HasClaim("imp_active", "1"))
            {
                await _next(context);
                return;
            }

            // تخطي الملفات والمسارات الاستثنائية
            var path = context.Request.Path.Value ?? string.Empty;
            if (_skipPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            // جلب المستخدم وتحقق من الحالة
            var user = await userManager.GetUserAsync(context.User);

            if (user != null && !user.IsActive)
            {
                await signInManager.SignOutAsync();

                // مسح الجلسة
                context.Session.Clear();

                var loginUrl = "/LMS/login?reason=suspended";

                if (IsAjaxRequest(context.Request))
                {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"account_suspended\",\"message\":\"تم إيقاف حسابك\"}");
                    return;
                }

                context.Response.Redirect(loginUrl);
                return;
            }

            // ✅ سلب صلاحية الدخول لاريا الطالب (مستقل عن IsActive) — قطع الجلسة فوراً
            if (user != null)
            {
                var studentAccess = await dbContext.Students
                    .AsNoTracking()
                    .Where(s => s.UserId == user.Id)
                    .Select(s => (bool?)s.CanAccessStudentArea)
                    .FirstOrDefaultAsync();

                if (studentAccess.HasValue && !studentAccess.Value)
                {
                    await signInManager.SignOutAsync();
                    context.Session.Clear();

                    var accessRevokedUrl = "/LMS/login?reason=access_revoked";

                    if (IsAjaxRequest(context.Request))
                    {
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("{\"error\":\"access_revoked\",\"message\":\"تم إيقاف صلاحية الدخول لحسابك\"}");
                        return;
                    }

                    context.Response.Redirect(accessRevokedUrl);
                    return;
                }
            }

            await _next(context);
        }

        private static bool IsAjaxRequest(HttpRequest request)
        {
            return request.Headers["X-Requested-With"] == "XMLHttpRequest"
                || (request.Headers["Accept"].ToString().Contains("application/json")
                    && !request.Headers["Accept"].ToString().Contains("text/html"));
        }
    }
}
