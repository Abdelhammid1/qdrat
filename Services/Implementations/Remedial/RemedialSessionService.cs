using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations.Remedial
{
    public class RemedialSessionService : IRemedialSessionService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public RemedialSessionService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<RemedialSession>> GetAllAsync()
        {
            using var _context = _contextFactory.CreateDbContext();
            return await _context.RemedialSessions
                .Include(r => r.Student)
                .Include(r => r.RemedialPlan)
                .Include(r => r.Section)
                .ToListAsync();
        }

        public async Task<RemedialSession?> GetByIdAsync(int id)
        {
            using var _context = _contextFactory.CreateDbContext();
            return await _context.RemedialSessions
                .Include(r => r.Student)
                .Include(r => r.RemedialPlan)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<bool> CreateAsync(RemedialSession session)
        {
            using var _context = _contextFactory.CreateDbContext();
            session.CreatedAt = DateTime.UtcNow;
            _context.RemedialSessions.Add(session);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateAsync(RemedialSession session)
        {
            using var _context = _contextFactory.CreateDbContext();
            _context.RemedialSessions.Update(session);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var _context = _contextFactory.CreateDbContext();
            var entity = await _context.RemedialSessions.FindAsync(id);
            if (entity == null) return false;
            _context.RemedialSessions.Remove(entity);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
