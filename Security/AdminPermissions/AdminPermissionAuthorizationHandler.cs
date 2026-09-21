using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using System.Security.Claims;

namespace QdratNew.Security.AdminPermissions
{
    public class AdminPermissionAuthorizationHandler
        : AuthorizationHandler<AdminPermissionAuthorizationRequirement>
    {
        private readonly ApplicationDbContext _context;

        public AdminPermissionAuthorizationHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            AdminPermissionAuthorizationRequirement requirement)
        {
            if (context.User?.Identity?.IsAuthenticated != true)
                return;

            if (
                context.User.IsInRole("SuperAdmin") ||
                context.User.IsInRole("Owner") ||
                context.User.IsInRole("Developer")
            )
            {
                context.Succeed(requirement);
                return;
            }

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return;

            var adminProfileId = await _context.AdminUserProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.AdminProfileId)
                .FirstOrDefaultAsync();

            if (adminProfileId <= 0)
                return;

            var permission = await _context.AdminProfileControllerPermissions
                .Include(p => p.CustomPermissions)
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.AdminProfileId == adminProfileId &&
                    p.ControllerName == requirement.ControllerName);

            if (permission == null)
                return;

            var hasCustomPermissions = permission.HasCustomPermissions;

            var customAllowed = permission.CustomPermissions != null &&
                                permission.CustomPermissions.Any(c =>
                                    c.ActionName == requirement.ActionName &&
                                    c.IsAllowed);

            bool allowed;

            if (requirement.ActionName == "Read")
            {
                allowed =
                    permission.AccessLevel >= AdminControllerAccessLevel.Read ||
                    customAllowed;
            }
            else
            {
                if (hasCustomPermissions)
                {
                    allowed = customAllowed;
                }
                else
                {
                    allowed = permission.AccessLevel >= AdminControllerAccessLevel.ReadWrite;
                }
            }

            if (allowed)
            {
                context.Succeed(requirement);
            }
        }
    }
}