using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using System.Security.Claims;

namespace QdratNew.Services.Admin
{
    public class EmployeeBatchAccessService : IEmployeeBatchAccessService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public EmployeeBatchAccessService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public bool IsPrivilegedUser(ClaimsPrincipal user)
            => user.IsInRole("Owner") || user.IsInRole("Developer") || user.IsInRole("SuperAdmin");

        public async Task<bool> HasFullBatchesAccessAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            using var context = _contextFactory.CreateDbContext();

            var adminProfileId = await context.AdminUserProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.AdminProfileId)
                .FirstOrDefaultAsync();

            if (adminProfileId <= 0)
                return false;

            var permission = await context.AdminProfileControllerPermissions
                .Include(p => p.CustomPermissions)
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.AdminProfileId == adminProfileId &&
                    p.ControllerName == "Batches");

            if (permission == null)
                return false;

            // نفس منطق AdminPermissionAuthorizationHandler لصلاحية "Read":
            // مسموح إذا كان مستوى الوصول Read/ReadWrite، أو إذا كانت "Read" مفعّلة ضمن صلاحيات Custom.
            var customReadAllowed = permission.CustomPermissions != null &&
                                     permission.CustomPermissions.Any(c =>
                                         c.ActionName == "Read" && c.IsAllowed);

            return permission.AccessLevel >= AdminControllerAccessLevel.Read || customReadAllowed;
        }

        public async Task<List<int>> GetPermittedBatchIdsAsync(string userId, InstructorBatchFeature feature)
        {
            using var context = _contextFactory.CreateDbContext();

            return await context.EmployeeBatchAccesses
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Feature == feature && x.IsGranted)
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();
        }

        public async Task<bool> HasBatchFeatureAccessAsync(string userId, int batchId, InstructorBatchFeature feature)
        {
            using var context = _contextFactory.CreateDbContext();

            return await context.EmployeeBatchAccesses
                .AsNoTracking()
                .AnyAsync(x => x.UserId == userId &&
                               x.BatchId == batchId &&
                               x.Feature == feature &&
                               x.IsGranted);
        }
    }
}
