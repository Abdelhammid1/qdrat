using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Services.RemedialTracks
{
    public sealed class RemedialTrackAuditService : IRemedialTrackAuditService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly ILogger<RemedialTrackAuditService> _logger;

        public RemedialTrackAuditService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            ILogger<RemedialTrackAuditService> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _logger = logger;
        }

        public async Task LogAsync(int trackId, string actionType, string description, ClaimsPrincipal user, string? actorName, CancellationToken ct = default)
        {
            try
            {
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                if (trackId <= 0 || string.IsNullOrWhiteSpace(userId)) return;

                await using var db = await _dbFactory.CreateDbContextAsync(ct);

                var profileName = await db.AdminUserProfiles.AsNoTracking()
                    .Where(x => x.UserId == userId)
                    .Select(x => x.AdminProfile!.Name)
                    .FirstOrDefaultAsync(ct);

                var roles = string.Join("،", user.FindAll(ClaimTypes.Role).Select(r => r.Value).Distinct());

                db.RemedialTrackAuditLogs.Add(new RemedialTrackAuditLog
                {
                    TrackId = trackId,
                    ActionType = Truncate(actionType, 60),
                    Description = Truncate(description, 600),
                    ActorUserId = Truncate(userId, 450),
                    ActorName = Truncate(actorName ?? user.Identity?.Name ?? string.Empty, 200),
                    ActorProfileName = profileName is null ? null : Truncate(profileName, 200),
                    ActorRoles = roles.Length == 0 ? null : Truncate(roles, 100),
                    CreatedAtUtc = _time.GetUtcNow().UtcDateTime
                });
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RTK: فشل تسجيل سجل تعديل الخطة (track {TrackId}, {Action})", trackId, actionType);
            }
        }

        public async Task<int?> TrackIdOfAxisAsync(int axisId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await db.RemedialTrackAxes.AsNoTracking().Where(a => a.Id == axisId).Select(a => (int?)a.TrackId).FirstOrDefaultAsync(ct);
        }

        public async Task<int?> TrackIdOfVideoAsync(int videoId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await db.RemedialTrackVideos.AsNoTracking().Where(v => v.Id == videoId).Select(v => (int?)v.Axis!.TrackId).FirstOrDefaultAsync(ct);
        }

        public async Task<int?> TrackIdOfPublicationAsync(int publicationId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await db.RemedialTrackPublications.AsNoTracking().Where(p => p.Id == publicationId).Select(p => (int?)p.TrackId).FirstOrDefaultAsync(ct);
        }

        public async Task<int?> TrackIdOfAddendumAsync(int addendumId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await db.RemedialTrackAddenda.AsNoTracking().Where(a => a.Id == addendumId).Select(a => (int?)a.Publication!.TrackId).FirstOrDefaultAsync(ct);
        }

        public async Task<int?> TrackIdOfEnrollmentAsync(int enrollmentId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await db.RemedialTrackEnrollments.AsNoTracking().Where(e => e.Id == enrollmentId).Select(e => (int?)e.Publication!.TrackId).FirstOrDefaultAsync(ct);
        }

        public async Task<List<RemedialTrackAuditEntryVm>> GetAsync(int trackId, int take = 100, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await db.RemedialTrackAuditLogs.AsNoTracking()
                .Where(x => x.TrackId == trackId)
                .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
                .Take(Math.Clamp(take, 1, 500))
                .Select(x => new RemedialTrackAuditEntryVm
                {
                    CreatedAtUtc = x.CreatedAtUtc,
                    ActionType = x.ActionType,
                    Description = x.Description,
                    ActorName = x.ActorName ?? string.Empty,
                    ActorProfileName = x.ActorProfileName,
                    ActorRoles = x.ActorRoles
                })
                .ToListAsync(ct);
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
    }
}
