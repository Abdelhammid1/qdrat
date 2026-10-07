using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S11.3: صفحة ولي الأمر. الملكية من الخادم: التقرير ظاهر فقط إن كان Sent ومنسوبًا لولي الأمر الحالي،
    /// والطالب ما زال مرتبطًا به، وأمر النشر غير محذوف. القراءة من SnapshotJson (لا إعادة حساب حي) وبـ AsNoTracking.
    /// </summary>
    public sealed class RemedialTrackParentReportService : IRemedialTrackParentReportService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly IAdminActivityLogger _activity;
        private readonly ILogger<RemedialTrackParentReportService> _logger;

        public RemedialTrackParentReportService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            IAdminActivityLogger activity,
            ILogger<RemedialTrackParentReportService> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _activity = activity;
            _logger = logger;
        }

        // شرط الملكية الموحّد (لا Contains على قوائم؛ EXISTS على الطلاب)
        private static IQueryable<RemedialTrackParentReport> Owned(ApplicationDbContext db, int parentId)
            => db.RemedialTrackParentReports
                .Where(r => r.ParentId == parentId
                            && r.Status == RemedialTrackParentReportStatus.Sent
                            && !r.Enrollment!.Publication!.IsDeleted
                            && db.Students.Any(s => s.StudentID == r.StudentId && s.ParentId == parentId));

        public async Task<RemedialTrackParentReportListVm> GetListAsync(int parentId, int page, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var q = Owned(db, parentId).AsNoTracking();
            var total = await q.CountAsync(ct);
            var totalPages = total <= 0 ? 1 : (int)Math.Ceiling(total / (double)RemedialTrackParentReportListVm.PageSize);
            var current = Math.Clamp(page, 1, totalPages);

            var items = await q
                .OrderByDescending(r => r.SentAtUtc).ThenByDescending(r => r.Id)
                .Skip((current - 1) * RemedialTrackParentReportListVm.PageSize)
                .Take(RemedialTrackParentReportListVm.PageSize)
                .Select(r => new RemedialTrackParentReportListItemVm
                {
                    Id = r.Id,
                    Kind = r.Kind,
                    StudentName = r.Enrollment!.Student!.FullName,
                    TrackTitle = r.Enrollment.Publication!.Track!.Title,
                    FocusAxisTitle = db.RemedialTrackAxisProgresses
                        .Where(a => a.Id == r.AxisProgressId)
                        .Select(a => a.Axis!.TitleOverride ?? a.Axis.Section!.Title)
                        .FirstOrDefault(),
                    SentAtUtc = r.SentAtUtc ?? r.CreatedAtUtc,
                    AcknowledgedAtUtc = r.AcknowledgedAtUtc
                })
                .ToListAsync(ct);

            return new RemedialTrackParentReportListVm { Items = items, Page = current, Total = total };
        }

        public async Task<RemedialTrackParentReportDetailsVm?> GetDetailsAsync(int parentId, int reportId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var row = await Owned(db, parentId).AsNoTracking()
                .Where(r => r.Id == reportId)
                .Select(r => new { r.Id, r.Kind, r.SnapshotJson, r.SentAtUtc, r.CreatedAtUtc, r.AcknowledgedAtUtc })
                .FirstOrDefaultAsync(ct);
            if (row is null) return null;

            var snapshot = RemedialTrackParentReportWriter.TryDeserialize(row.SnapshotJson);
            if (snapshot is null)
            {
                _logger.LogWarning("RTK: لقطة تقرير ولي الأمر {ReportId} غير صالحة للعرض", row.Id);
                return null;
            }

            return new RemedialTrackParentReportDetailsVm
            {
                Id = row.Id,
                Kind = row.Kind,
                SentAtUtc = row.SentAtUtc ?? row.CreatedAtUtc,
                AcknowledgedAtUtc = row.AcknowledgedAtUtc,
                Snapshot = snapshot
            };
        }

        public async Task<RemedialTrackParentAckResult> AcknowledgeAsync(
            int parentId, int reportId, string? userId, string? userName, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var report = await Owned(db, parentId).FirstOrDefaultAsync(r => r.Id == reportId, ct);
            if (report is null) return new RemedialTrackParentAckResult(false, false, null);
            if (report.AcknowledgedAtUtc.HasValue)
                return new RemedialTrackParentAckResult(true, true, report.AcknowledgedAtUtc);

            var now = _time.GetUtcNow().UtcDateTime;
            report.AcknowledgedAtUtc = now;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // إقرار متزامن من نفس ولي الأمر: أعد القراءة وتعامل معه كمُقرّ سابقًا
                await using var db2 = await _dbFactory.CreateDbContextAsync(ct);
                var acked = await Owned(db2, parentId).AsNoTracking()
                    .Where(r => r.Id == reportId).Select(r => r.AcknowledgedAtUtc).FirstOrDefaultAsync(ct);
                return new RemedialTrackParentAckResult(true, true, acked);
            }

            try
            {
                var ctx = await db.RemedialTrackEnrollments.AsNoTracking()
                    .Where(e => e.Id == report.EnrollmentId)
                    .Select(e => new { e.PublicationId, e.Publication!.BatchId })
                    .FirstOrDefaultAsync(ct);
                await _activity.LogAsync(
                    "RemedialTrack.ParentAcknowledged",
                    $"[RTK pub:{ctx?.PublicationId}] أقرّ ولي الأمر بالاطلاع على تقرير الخطة العلاجية {report.Id} (تسجيل {report.EnrollmentId}).",
                    userId, userName, studentId: report.StudentId, batchId: ctx?.BatchId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RTK: فشل تسجيل نشاط إقرار ولي الأمر (تقرير {ReportId})", report.Id);
            }

            return new RemedialTrackParentAckResult(true, false, now);
        }
    }
}
