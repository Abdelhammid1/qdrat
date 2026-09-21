using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Parents.Interfaces;
using System.Threading.Tasks;

namespace QdratNew.Services.Parents.Implementations
{
    public class ParentAccessService : IParentAccessService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ParentAccessService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<int?> GetCurrentParentIdAsync(string userId)
        {
            using var db = _contextFactory.CreateDbContext();
            var parent = await db.Parents
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => (int?)p.ParentID)
                .FirstOrDefaultAsync();
            return parent;
        }

        public async Task<bool> CanAccessStudentAsync(string parentUserId, int studentId)
        {
            using var db = _contextFactory.CreateDbContext();
            var parentId = await GetCurrentParentIdAsync(parentUserId);
            if (parentId == null) return false;

            return await db.Students
                .AsNoTracking()
                .AnyAsync(s => s.StudentID == studentId && s.ParentId == parentId);
        }
    }
}
