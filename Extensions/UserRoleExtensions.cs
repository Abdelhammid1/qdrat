using System.Security.Claims;

namespace QdratNew.Extensions
{
    public static class UserRoleExtensions
    {
        public static bool IsCoreSystemUser(this ClaimsPrincipal user)
        {
            return user.IsInRole("Owner")
                || user.IsInRole("SuperAdmin")
                || user.IsInRole("Developer");
        }
    }
}
