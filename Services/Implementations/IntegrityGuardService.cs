using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Implementations
{
    public class IntegrityGuardService : IIntegrityGuardService
    {
        private readonly ApplicationDbContext _context;

        public IntegrityGuardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsBlockedAsync(int studentId, IntegrityAttemptType attemptType, int attemptEntityId)
        {
            if (studentId <= 0) return false;

            return await _context.IntegrityViolationLogs
                .AsNoTracking()
                .AnyAsync(v => v.StudentId == studentId
                            && v.AttemptType == attemptType
                            && v.AttemptEntityId == attemptEntityId
                            && !v.IsResolved);
        }

        private static readonly TimeSpan MinIntervalBetweenViolations = TimeSpan.FromSeconds(5);

        public async Task LogViolationAsync(
            int studentId,
            IntegrityAttemptType attemptType,
            int attemptEntityId,
            string violationType,
            string? ipAddress,
            string? userAgent,
            string? pageUrl)
        {
            if (studentId <= 0) return;

            var alreadyLogged = await _context.IntegrityViolationLogs
                .AsNoTracking()
                .AnyAsync(v => v.StudentId == studentId
                            && v.AttemptType == attemptType
                            && v.AttemptEntityId == attemptEntityId
                            && !v.IsResolved);
            if (alreadyLogged) return;

            var lastDetectedAt = await _context.IntegrityViolationLogs
                .AsNoTracking()
                .Where(v => v.StudentId == studentId)
                .OrderByDescending(v => v.DetectedAt)
                .Select(v => v.DetectedAt)
                .FirstOrDefaultAsync();
            if (lastDetectedAt != default && DateTime.UtcNow - lastDetectedAt < MinIntervalBetweenViolations) return;

            _context.IntegrityViolationLogs.Add(new IntegrityViolationLog
            {
                StudentId = studentId,
                AttemptType = attemptType,
                AttemptEntityId = attemptEntityId,
                ViolationType = string.IsNullOrWhiteSpace(violationType) ? "BrowserTranslate" : violationType,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                PageUrl = pageUrl
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // فهرس فريد مُصفّى يمنع تكرار صف نشط لنفس المحاولة عند طلبين متزامنين (Race Condition) — يُتجاهَل بأمان.
            }
        }

        public async Task<bool> ResolveAsync(int violationLogId, string adminUserId, string? note)
        {
            var log = await _context.IntegrityViolationLogs.FindAsync(violationLogId);
            if (log == null) return false;

            log.IsResolved = true;
            log.ResolvedAt = DateTime.UtcNow;
            log.ResolvedByAdminId = adminUserId;
            log.ResolutionNote = note;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CanSelfResolveAsync(int studentId, IntegrityAttemptType attemptType, int attemptEntityId)
        {
            if (studentId <= 0) return false;

            var alreadyUsed = await _context.IntegrityViolationLogs
                .AsNoTracking()
                .AnyAsync(v => v.StudentId == studentId
                            && v.AttemptType == attemptType
                            && v.AttemptEntityId == attemptEntityId
                            && v.SelfResolved);

            return !alreadyUsed;
        }

        public async Task<(bool Success, string Message)> TrySelfResolveAsync(int studentId, IntegrityAttemptType attemptType, int attemptEntityId)
        {
            if (studentId <= 0) return (false, "بيانات غير صالحة");

            var activeLog = await _context.IntegrityViolationLogs
                .Where(v => v.StudentId == studentId
                         && v.AttemptType == attemptType
                         && v.AttemptEntityId == attemptEntityId
                         && !v.IsResolved)
                .OrderByDescending(v => v.DetectedAt)
                .FirstOrDefaultAsync();

            if (activeLog == null)
                return (true, "المحاولة غير موقوفة بالفعل، يمكنك المتابعة.");

            var alreadyUsedSelfResolve = await _context.IntegrityViolationLogs
                .AsNoTracking()
                .AnyAsync(v => v.StudentId == studentId
                            && v.AttemptType == attemptType
                            && v.AttemptEntityId == attemptEntityId
                            && v.SelfResolved);

            if (alreadyUsedSelfResolve)
                return (false, "تم استخدام إمكانية العودة الذاتية من قبل لهذه المحاولة. تواصل مع المشرف على المنصة لإعادة فتحها.");

            activeLog.IsResolved = true;
            activeLog.ResolvedAt = DateTime.UtcNow;
            activeLog.SelfResolved = true;
            activeLog.ResolutionNote = "إعادة فتح ذاتية من الطالب (مسموح مرة واحدة فقط)";

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return (false, "تعذّر إعادة فتح المحاولة، حاول مرة أخرى.");
            }

            return (true, "تم فتح المحاولة، يمكنك المتابعة الآن.");
        }

        public async Task<bool> ResolveActiveAsync(int studentId, IntegrityAttemptType attemptType, int attemptEntityId, string adminUserId, string? note)
        {
            var activeLog = await _context.IntegrityViolationLogs
                .Where(v => v.StudentId == studentId
                         && v.AttemptType == attemptType
                         && v.AttemptEntityId == attemptEntityId
                         && !v.IsResolved)
                .OrderByDescending(v => v.DetectedAt)
                .FirstOrDefaultAsync();

            if (activeLog == null) return false;

            activeLog.IsResolved = true;
            activeLog.ResolvedAt = DateTime.UtcNow;
            activeLog.ResolvedByAdminId = adminUserId;
            activeLog.ResolutionNote = note;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
