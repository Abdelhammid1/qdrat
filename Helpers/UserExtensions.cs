using System.Security.Claims;

namespace QdratNew.Helpers
{
    public static class UserExtensions
    {
        public static string? GetUserId(this ClaimsPrincipal user)
        {
            return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        public static int GetUserIdAsInt(this ClaimsPrincipal user)
        {
            var id = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(id, out int result) ? result : 0;
        }
    }
}
