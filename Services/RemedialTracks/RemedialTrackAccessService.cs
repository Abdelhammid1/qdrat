using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S4.2: بوابة الوصول (D14) والتحقق من الرقم المرجعي (D6/D8).
    /// التحقق يُخزَّن في RemedialTrackEnrollment.VerifiedCodeVersion — لا Session.
    /// </summary>
    public sealed class RemedialTrackAccessService : IRemedialTrackAccessService
    {
        public const int MaxFailedAttempts = 5;                                  // D8
        public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15); // D8

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;

        public RemedialTrackAccessService(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider time)
        {
            _dbFactory = dbFactory;
            _time = time;
        }

        // ───────────────────────── القرار النقي ─────────────────────────

        /// <summary>قرار البوابة من لقطة جاهزة (دالة نقية قابلة للاختبار جدوليًا).</summary>
        public static RemedialTrackAccessOutcome Decide(RemedialTrackAccessSnapshot s, int studentId, DateTime nowUtc)
        {
            if (s.StudentId != studentId) return RemedialTrackAccessOutcome.NotFound;                       // لا تكشف وجوده لغيره
            if (s.PublicationDeleted
                || s.PublicationStatus != RemedialTrackPublicationStatus.Active
                || s.EnrollmentStatus == RemedialTrackEnrollmentStatus.Cancelled)
                return RemedialTrackAccessOutcome.Cancelled;
            if (s.PublishAtUtc > nowUtc) return RemedialTrackAccessOutcome.NotYetPublished;

            if (s.Mode == RemedialTrackDeliveryMode.InPerson && s.VerifiedCodeVersion != s.CodeVersion)
            {
                return s.CodeLockedUntilUtc.HasValue && s.CodeLockedUntilUtc.Value > nowUtc
                    ? RemedialTrackAccessOutcome.CodeLocked
                    : RemedialTrackAccessOutcome.NeedsCode;
            }

            return RemedialTrackAccessOutcome.Allowed;
        }

        /// <summary>يحوّل الأرقام الهندية/الفارسية إلى لاتينية ويقصّ الفراغات؛ يعيد null إن لم يكن 6 أرقام بالضبط.</summary>
        public static string? NormalizeCode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var sb = new StringBuilder(6);
            foreach (var ch in raw.Trim())
            {
                if (ch >= '0' && ch <= '9') sb.Append(ch);
                else if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));   // ٠-٩
                else if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));   // ۰-۹
                else return null;
                if (sb.Length > 6) return null;
            }
            return sb.Length == 6 ? sb.ToString() : null;
        }

        // ───────────────────────── Evaluate ─────────────────────────

        public async Task<RemedialTrackAccessResult> EvaluateAsync(int studentId, int enrollmentId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var row = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.Id == enrollmentId && e.StudentId == studentId)
                .Select(e => new
                {
                    Snapshot = new RemedialTrackAccessSnapshot(
                        e.StudentId, e.Status, e.Publication!.Status, e.Publication.PublishAtUtc, e.Publication.Mode,
                        e.Publication.CodeVersion, e.VerifiedCodeVersion, e.CodeLockedUntilUtc, e.Publication.IsDeleted),
                    e.StartedAtUtc,
                    e.TermsAcceptedAtUtc,
                    Title = e.Publication.Track!.Title
                })
                .FirstOrDefaultAsync(ct);

            if (row is null) return new RemedialTrackAccessResult(RemedialTrackAccessOutcome.NotFound);

            var outcome = Decide(row.Snapshot, studentId, now);
            if (outcome != RemedialTrackAccessOutcome.Allowed)
            {
                var until = outcome == RemedialTrackAccessOutcome.CodeLocked ? row.Snapshot.CodeLockedUntilUtc : null;
                return new RemedialTrackAccessResult(outcome, until, row.Title);
            }

            // الإقرار مطلوب لكل تسجيل لم يوافق بعد (حتى من بدأ الخطة قبل إضافة الإقرار) لتوثيق الموافقة
            if (row.TermsAcceptedAtUtc is null)
                return new RemedialTrackAccessResult(RemedialTrackAccessOutcome.NeedsTerms, null, row.Title);

            if (row.StartedAtUtc is null)
                await MarkStartedAsync(db, enrollmentId, now, ct);

            return new RemedialTrackAccessResult(RemedialTrackAccessOutcome.Allowed, null, row.Title);
        }

        // ───────────────────────── AcceptTerms ─────────────────────────

        public async Task<bool> AcceptTermsAsync(int studentId, int enrollmentId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var row = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.Id == enrollmentId && e.StudentId == studentId)
                .Select(e => new { e.Status, PubStatus = e.Publication!.Status, e.Publication.IsDeleted, e.Publication.PublishAtUtc, e.TermsAcceptedAtUtc })
                .FirstOrDefaultAsync(ct);

            if (row is null
                || row.IsDeleted
                || row.PubStatus != RemedialTrackPublicationStatus.Active
                || row.Status == RemedialTrackEnrollmentStatus.Cancelled
                || row.PublishAtUtc > now)
                return false;

            if (row.TermsAcceptedAtUtc.HasValue) return true;   // مُقَرّ سابقًا — idempotent

            if (db.Database.IsRelational())
            {
                var updated = await db.RemedialTrackEnrollments
                    .Where(e => e.Id == enrollmentId && e.TermsAcceptedAtUtc == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.TermsAcceptedAtUtc, (DateTime?)now), ct);
                if (updated == 0) return true;                   // سبقه طلب آخر
            }
            else
            {
                var e = await db.RemedialTrackEnrollments.FirstAsync(x => x.Id == enrollmentId, ct);
                e.TermsAcceptedAtUtc = now;
                await db.SaveChangesAsync(ct);
            }

            db.RemedialTrackEvents.Add(new RemedialTrackEvent
            {
                EnrollmentId = enrollmentId,
                Type = RemedialTrackEventType.TermsAccepted,
                Message = "أقرّ الطالب بشروط وآلية الخطة العلاجية.",
                CreatedAtUtc = now
            });
            await db.SaveChangesAsync(ct);
            return true;
        }

        // أول وصول مسموح: StartedAtUtc + InProgress (تحديث ذرّي — أول من يصل يفوز)
        private static async Task MarkStartedAsync(ApplicationDbContext db, int enrollmentId, DateTime now, CancellationToken ct)
        {
            if (db.Database.IsRelational())
            {
                await db.RemedialTrackEnrollments
                    .Where(e => e.Id == enrollmentId && e.StartedAtUtc == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(e => e.StartedAtUtc, now)
                        .SetProperty(e => e.Status, RemedialTrackEnrollmentStatus.InProgress), ct);
                return;
            }

            var entity = await db.RemedialTrackEnrollments.FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StartedAtUtc == null, ct);
            if (entity is null) return;
            entity.StartedAtUtc = now;
            entity.Status = RemedialTrackEnrollmentStatus.InProgress;
            await db.SaveChangesAsync(ct);
        }

        // ───────────────────────── VerifyCode ─────────────────────────

        public async Task<RemedialTrackVerifyResult> VerifyCodeAsync(int studentId, int enrollmentId, string? code, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var row = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.Id == enrollmentId && e.StudentId == studentId)
                .Select(e => new
                {
                    e.Status,
                    PubStatus = e.Publication!.Status,
                    e.Publication.IsDeleted,
                    e.Publication.PublishAtUtc,
                    e.Publication.Mode,
                    e.Publication.AccessCode,
                    e.Publication.CodeVersion,
                    e.CodeLockedUntilUtc
                })
                .FirstOrDefaultAsync(ct);

            if (row is null) return new RemedialTrackVerifyResult(RemedialTrackVerifyOutcome.NotFound);
            if (row.IsDeleted || row.PubStatus != RemedialTrackPublicationStatus.Active || row.Status == RemedialTrackEnrollmentStatus.Cancelled)
                return new RemedialTrackVerifyResult(RemedialTrackVerifyOutcome.Cancelled);
            if (row.PublishAtUtc > now) return new RemedialTrackVerifyResult(RemedialTrackVerifyOutcome.NotYetPublished);

            // الأونلاين لا يحتاج رقمًا
            if (row.Mode != RemedialTrackDeliveryMode.InPerson)
                return new RemedialTrackVerifyResult(RemedialTrackVerifyOutcome.Verified);

            // مقفول: لا تنفّذ المقارنة أصلًا
            if (row.CodeLockedUntilUtc.HasValue && row.CodeLockedUntilUtc.Value > now)
                return new RemedialTrackVerifyResult(RemedialTrackVerifyOutcome.Locked, row.CodeLockedUntilUtc);

            var normalized = NormalizeCode(code);
            if (normalized is null || string.IsNullOrEmpty(row.AccessCode))
                return new RemedialTrackVerifyResult(RemedialTrackVerifyOutcome.Invalid);

            var match = CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(normalized), Encoding.ASCII.GetBytes(row.AccessCode));

            if (match)
            {
                await RecordSuccessAsync(db, enrollmentId, row.CodeVersion, now, ct);
                return new RemedialTrackVerifyResult(RemedialTrackVerifyOutcome.Verified);
            }

            var lockedUntil = await RecordFailureAsync(db, enrollmentId, now, ct);
            return lockedUntil.HasValue
                ? new RemedialTrackVerifyResult(RemedialTrackVerifyOutcome.Locked, lockedUntil)
                : new RemedialTrackVerifyResult(RemedialTrackVerifyOutcome.Invalid);
        }

        private static async Task RecordSuccessAsync(ApplicationDbContext db, int enrollmentId, int codeVersion, DateTime now, CancellationToken ct)
        {
            if (db.Database.IsRelational())
            {
                await db.RemedialTrackEnrollments
                    .Where(e => e.Id == enrollmentId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(e => e.VerifiedCodeVersion, (int?)codeVersion)
                        .SetProperty(e => e.FailedCodeAttempts, 0)
                        .SetProperty(e => e.CodeLockedUntilUtc, (DateTime?)null), ct);
            }
            else
            {
                var e = await db.RemedialTrackEnrollments.FirstAsync(x => x.Id == enrollmentId, ct);
                e.VerifiedCodeVersion = codeVersion;
                e.FailedCodeAttempts = 0;
                e.CodeLockedUntilUtc = null;
                await db.SaveChangesAsync(ct);
            }

            db.RemedialTrackEvents.Add(new RemedialTrackEvent
            {
                EnrollmentId = enrollmentId,
                Type = RemedialTrackEventType.CodeVerified,
                Message = "تحقق الطالب من الرقم المرجعي.",
                CreatedAtUtc = now
            });
            await db.SaveChangesAsync(ct);
        }

        /// <summary>يزيد العدّاد ذريًا؛ عند 5 يقفل 15 دقيقة ويصفّر العدّاد. يعيد وقت القفل إن حصل.</summary>
        private static async Task<DateTime?> RecordFailureAsync(ApplicationDbContext db, int enrollmentId, DateTime now, CancellationToken ct)
        {
            DateTime? lockedUntil = null;
            int attempts;

            if (db.Database.IsRelational())
            {
                await db.RemedialTrackEnrollments
                    .Where(e => e.Id == enrollmentId)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.FailedCodeAttempts, e => e.FailedCodeAttempts + 1), ct);

                attempts = await db.RemedialTrackEnrollments.AsNoTracking()
                    .Where(e => e.Id == enrollmentId).Select(e => e.FailedCodeAttempts).FirstAsync(ct);

                if (attempts >= MaxFailedAttempts)
                {
                    lockedUntil = now + LockDuration;
                    await db.RemedialTrackEnrollments
                        .Where(e => e.Id == enrollmentId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(e => e.CodeLockedUntilUtc, (DateTime?)lockedUntil)
                            .SetProperty(e => e.FailedCodeAttempts, 0), ct);
                }
            }
            else
            {
                var e = await db.RemedialTrackEnrollments.FirstAsync(x => x.Id == enrollmentId, ct);
                e.FailedCodeAttempts++;
                attempts = e.FailedCodeAttempts;
                if (attempts >= MaxFailedAttempts)
                {
                    lockedUntil = now + LockDuration;
                    e.CodeLockedUntilUtc = lockedUntil;
                    e.FailedCodeAttempts = 0;
                }
                await db.SaveChangesAsync(ct);
            }

            // لا نكتب الرقم المُدخل في السجل
            db.RemedialTrackEvents.Add(new RemedialTrackEvent
            {
                EnrollmentId = enrollmentId,
                Type = RemedialTrackEventType.CodeFailed,
                Message = lockedUntil.HasValue
                    ? "محاولات رقم مرجعي خاطئة متتالية — قُفل الإدخال مؤقتًا."
                    : "محاولة رقم مرجعي خاطئة.",
                CreatedAtUtc = now
            });
            await db.SaveChangesAsync(ct);
            return lockedUntil;
        }

        // ───────────────────────── القائمة الجانبية ─────────────────────────

        public async Task<bool> HasVisibleEnrollmentAsync(int studentId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;
            return await db.RemedialTrackEnrollments.AsNoTracking()
                .AnyAsync(e => e.StudentId == studentId
                               && e.Status != RemedialTrackEnrollmentStatus.Cancelled
                               && e.Publication!.Status == RemedialTrackPublicationStatus.Active
                               && !e.Publication.IsDeleted
                               && e.Publication.PublishAtUtc <= now, ct);
        }
    }
}
