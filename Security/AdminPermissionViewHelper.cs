using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Security.AdminPermissions;

namespace QdratNew.Security
{
    public static class AdminPermissionViewHelper
    {
        public static bool Can(
            HttpContext context,
            string controller,
            string action)
        {
            if (context?.User?.Identity?.IsAuthenticated != true)
                return false;

            // تكوين policy بنفس الصيغة المعتمدة في Requirement
            var policy = $"{controller}:{action}";

            var requirement = new AdminPermissionAuthorizationRequirement(policy);

            var authContext = new AuthorizationHandlerContext(
                new[] { requirement },
                context.User,
                resource: null
            );

            // تنفيذ جميع Authorization Handlers المسجلة
            var handlers = context.RequestServices
                .GetServices<IAuthorizationHandler>();

            foreach (var handler in handlers)
            {
                handler.HandleAsync(authContext).GetAwaiter().GetResult();
            }

            return authContext.HasSucceeded;
        }
    }
}
