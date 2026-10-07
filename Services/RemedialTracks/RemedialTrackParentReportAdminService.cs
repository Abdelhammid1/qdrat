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
    /// RTK-S12.1: طابور تقارير أولياء الأمور للأدمن. المنطق هنا والمتحكّم رفيع.
    /// الإرسال/الإعادة يكتبان حالة التقرير والإشعار في SaveChanges واحد (معاملة ضمنية)، و<c>RowVersion</c> يحمي من النقر المزدوج المتزامن.
    /// </summary>
    public sealed class RemedialTrackParentReportAdminService : IRemedialTrackParentReportAdminService
    {
        /// <summary>أقل فاصل بين إرسالين لنفس التقرير (حماية من تكرار الإشعار).</summary>
        public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

        private const int LookupLimit = 100;

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly IAdminActivityLogger _activity;
        private readonly ILogger<RemedialTrackParentReportAdminService> _logger;

        public RemedialTrackParentReportAdminService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            IAdminActivityLogger activity,
            ILogger<RemedialTrackParentReportAdminService> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _activity = activity;
            _logger = logger;
        }

        private DateTime Now => _time.GetUtcNow().UtcDateTime;

        // ======================================================================
        // الطابور
        // ======================================================================

        public async Task<RemedialTrackParentReportQueueVm> GetQueueAsync(
            RemedialTrackParentReportQueueFilter filter, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            // الأساس: غير المحذوف + ضمن نطاق الدفعات + فلتر الأمر/الدفعة (بمعزل عن فلتر الحالة لحساب العدّادات)
            var baseQ = db.RemedialTrackParentReports.AsNoTracking()
                .Where(r => !r.Enrollment!.Publication!.IsDeleted);
            if (scope.PermittedBatchIds is { } permitted)
            {
                var ids = permitted.ToList();   // قائمة صغيرة (دفعات الموظف) — نفس نمط GetIndexAsync
                baseQ = baseQ.Where(r => ids.Contains(r.Enrollment!.Publication!.BatchId));
            }
            if (filter.PublicationId is { } pubId)
                baseQ = baseQ.Where(r => r.Enrollment!.PublicationId == pubId);
            if (filter.BatchId is { } batchId)
                baseQ = baseQ.Where(r => r.Enrollment!.Publication!.BatchId == batchId);

            var counts = await baseQ
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var q = filter.Status is { } st ? baseQ.Where(r => r.Status == st) : baseQ;
            var total = await q.CountAsync(ct);
            var totalPages = total <= 0 ? 1 : (int)Math.Ceiling(total / (double)RemedialTrackParentReportQueueVm.PageSize);
            var current = Math.Clamp(filter.Page, 1, totalPages);

            var items = await q
                .OrderBy(r => r.Status == RemedialTrackParentReportStatus.Sent ? 1 : 0)   // غير المُرسل أولًا (يحتاج تدخّلًا)
                .ThenByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id)
                .Skip((current - 1) * RemedialTrackParentReportQueueVm.PageSize)
                .Take(RemedialTrackParentReportQueueVm.PageSize)
                .Select(r => new RemedialTrackParentReportQueueItemVm
                {
                    Id = r.Id,
                    EnrollmentId = r.EnrollmentId,
                    PublicationId = r.Enrollment!.PublicationId,
                    Kind = r.Kind,
                    Status = r.Status,
                    StudentName = r.Enrollment.Student!.FullName,
                    ParentName = r.Enrollment.Student.Parent != null ? r.Enrollment.Student.Parent.FullName : null,
                    TrackTitle = r.Enrollment.Publication!.Track!.Title,
                    BatchName = r.Enrollment.Publication.Batch!.Name,
                    FocusAxisTitle = db.RemedialTrackAxisProgresses
                        .Where(a => a.Id == r.AxisProgressId)
                        .Select(a => a.Axis!.TitleOverride ?? a.Axis.Section!.Title)
                        .FirstOrDefault(),
                    HasSnapshot = r.SnapshotJson != "{}",
                    CreatedAtUtc = r.CreatedAtUtc,
                    SentAtUtc = r.SentAtUtc,
                    AcknowledgedAtUtc = r.AcknowledgedAtUtc
                })
                .ToListAsync(ct);

            // قوائم الفلاتر (محدودة، ضمن النطاق): أوامر النشر غير المحذوفة والدفعات المرتبطة بها
            var pubQ = db.RemedialTrackPublications.AsNoTracking().Where(p => !p.IsDeleted);
            if (scope.PermittedBatchIds is { } permitted2)
            {
                var ids2 = permitted2.ToList();
                pubQ = pubQ.Where(p => ids2.Contains(p.BatchId));
            }
            var publications = await pubQ
                .OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id)
                .Take(LookupLimit)
                .Select(p => new { p.Id, p.BatchId, Title = p.Track!.Title, Batch = p.Batch!.Name })
                .ToListAsync(ct);

            return new RemedialTrackParentReportQueueVm
            {
                Items = items,
                Filter = new RemedialTrackParentReportQueueFilter
                {
                    Status = filter.Status, PublicationId = filter.PublicationId, BatchId = filter.BatchId, Page = current
                },
                Page = current,
                Total = total,
                StatusCounts = counts.ToDictionary(c => c.Status, c => c.Count),
                Publications = publications
                    .Select(p => new RemedialTrackParentReportQueueLookup { Id = p.Id, Name = $"{p.Title} — {p.Batch}" })
                    .ToList(),
                Batches = publications
                    .GroupBy(p => p.BatchId)
                    .Select(g => new RemedialTrackParentReportQueueLookup { Id = g.Key, Name = g.First().Batch })
                    .OrderBy(b => b.Name)
                    .ToList()
            };
        }

        // ======================================================================
        // الإجراءات
        // ======================================================================

        /// <summary>سياق التقرير مع نطاق الدفعة؛ null ← غير موجود/خارج النطاق/أمر محذوف (يُعامَل 404).</summary>
        private sealed record ReportContext(int PublicationId, int BatchId, int StudentId, int? CurrentParentId, string? CurrentParentUserId);

        private static async Task<ReportContext?> LoadContextAsync(
            ApplicationDbContext db, RemedialTrackParentReport report, RemedialTrackBatchScope scope, CancellationToken ct)
        {
            var ctx = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.Id == report.EnrollmentId)
                .Select(e => new
                {
                    e.PublicationId,
                    e.Publication!.BatchId,
                    e.Publication.IsDeleted,
                    e.StudentId,
                    ParentId = e.Student!.ParentId,
                    ParentUserId = e.Student.Parent != null ? e.Student.Parent.UserId : null
                })
                .FirstOrDefaultAsync(ct);
            if (ctx is null || ctx.IsDeleted || !scope.Allows(ctx.BatchId)) return null;
            return new ReportContext(ctx.PublicationId, ctx.BatchId, ctx.StudentId, ctx.ParentId, ctx.ParentUserId);
        }

        public async Task<RemedialTrackParentReportActionResult> SendAsync(
            int reportId, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var report = await db.RemedialTrackParentReports.FirstOrDefaultAsync(r => r.Id == reportId, ct);
            if (report is null) return RemedialTrackParentReportActionResult.NotFound;
            var ctx = await LoadContextAsync(db, report, scope, ct);
            if (ctx is null) return RemedialTrackParentReportActionResult.NotFound;

            if (report.Status == RemedialTrackParentReportStatus.Sent)
                return RemedialTrackParentReportActionResult.Fail("التقرير مُرسل مسبقًا. استخدم «إعادة إرسال الإشعار» إن لزم.");

            // ولي الأمر الحالي للطالب (قد يُربط بعد إنشاء التقرير فيُحوَّل NoParent إلى قابل للإرسال)
            if (!ctx.CurrentParentId.HasValue || string.IsNullOrEmpty(ctx.CurrentParentUserId))
                return RemedialTrackParentReportActionResult.Fail("لا يوجد ولي أمر مرتبط بهذا الطالب. اربط الطالب بولي أمر ثم أعد المحاولة.");

            var now = Now;
            string? axisTitle = null;
            if (report.SnapshotJson == "{}")
            {
                int? focusOrder = null;
                if (report.AxisProgressId.HasValue)
                    focusOrder = await db.RemedialTrackAxisProgresses.AsNoTracking()
                        .Where(a => a.Id == report.AxisProgressId.Value).Select(a => (int?)a.Order).FirstOrDefaultAsync(ct);

                var (json, title) = await RemedialTrackParentReportWriter.TryBuildSnapshotJsonAsync(db, report.EnrollmentId, focusOrder, now, _logger, ct);
                if (json is null)
                    return RemedialTrackParentReportActionResult.Fail("تعذّر بناء لقطة التقرير الآن. أعد المحاولة لاحقًا.");
                report.SnapshotJson = json;
                axisTitle = title;
            }
            else
            {
                axisTitle = RemedialTrackParentReportWriter.TryDeserialize(report.SnapshotJson)?.FocusAxisTitle;
            }

            var studentName = await db.Students.AsNoTracking()
                .Where(s => s.StudentID == ctx.StudentId).Select(s => s.FullName).FirstOrDefaultAsync(ct) ?? string.Empty;

            report.ParentId = ctx.CurrentParentId;
            report.Status = RemedialTrackParentReportStatus.Sent;
            report.SentAtUtc = now;
            report.SentByUserId = actor.UserId;

            RemedialTrackParentReportWriter.AddNotification(db, ctx.CurrentParentUserId, ctx.CurrentParentId.Value, ctx.StudentId,
                RemedialTrackParentCopy.NotificationMessage(report.Kind, studentName, axisTitle),
                RemedialTrackParentReportWriter.ReportUrl(report.Id), now);

            if (!await TrySaveAsync(db, ct))
                return RemedialTrackParentReportActionResult.Fail("تغيّرت حالة التقرير للتو. حدّث الصفحة وأعد المحاولة.");

            await LogAsync("RemedialTrack.ParentReportSent", ctx, report, actor,
                $"إرسال تقرير ولي الأمر {report.Id} (تسجيل {report.EnrollmentId}) يدويًا.");
            return RemedialTrackParentReportActionResult.Ok("✅ تم إرسال التقرير وإشعار ولي الأمر.");
        }

        public async Task<RemedialTrackParentReportActionResult> ResendAsync(
            int reportId, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var report = await db.RemedialTrackParentReports.FirstOrDefaultAsync(r => r.Id == reportId, ct);
            if (report is null) return RemedialTrackParentReportActionResult.NotFound;
            var ctx = await LoadContextAsync(db, report, scope, ct);
            if (ctx is null) return RemedialTrackParentReportActionResult.NotFound;

            if (report.Status != RemedialTrackParentReportStatus.Sent || !report.ParentId.HasValue)
                return RemedialTrackParentReportActionResult.Fail("إعادة الإرسال متاحة للتقارير المُرسلة فقط.");

            var now = Now;
            if (report.SentAtUtc.HasValue && now - report.SentAtUtc.Value < ResendCooldown)
                return RemedialTrackParentReportActionResult.Fail("⏳ أُرسل إشعار لهذا التقرير للتو. انتظر دقيقة قبل إعادة الإرسال.");

            var parentUserId = await db.Parents.AsNoTracking()
                .Where(p => p.ParentID == report.ParentId.Value).Select(p => p.UserId).FirstOrDefaultAsync(ct);
            if (string.IsNullOrEmpty(parentUserId))
                return RemedialTrackParentReportActionResult.Fail("تعذّر تحديد حساب ولي الأمر لإرسال الإشعار.");

            var studentName = await db.Students.AsNoTracking()
                .Where(s => s.StudentID == ctx.StudentId).Select(s => s.FullName).FirstOrDefaultAsync(ct) ?? string.Empty;
            var axisTitle = RemedialTrackParentReportWriter.TryDeserialize(report.SnapshotJson)?.FocusAxisTitle;

            report.SentAtUtc = now;           // يخدم أيضًا حارس الدقيقة؛ RowVersion يمنع نجاح نقرتين متزامنتين
            report.SentByUserId = actor.UserId;
            RemedialTrackParentReportWriter.AddNotification(db, parentUserId, report.ParentId.Value, ctx.StudentId,
                RemedialTrackParentCopy.NotificationMessage(report.Kind, studentName, axisTitle),
                RemedialTrackParentReportWriter.ReportUrl(report.Id), now);

            if (!await TrySaveAsync(db, ct))
                return RemedialTrackParentReportActionResult.Fail("⏳ أُرسل إشعار لهذا التقرير للتو. انتظر دقيقة قبل إعادة الإرسال.");

            await LogAsync("RemedialTrack.ParentReportResent", ctx, report, actor,
                $"إعادة إرسال إشعار تقرير ولي الأمر {report.Id} (تسجيل {report.EnrollmentId}).");
            return RemedialTrackParentReportActionResult.Ok("✅ أُعيد إرسال إشعار التقرير لولي الأمر.");
        }

        public async Task<RemedialTrackParentReportActionResult> SuppressAsync(
            int reportId, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var report = await db.RemedialTrackParentReports.FirstOrDefaultAsync(r => r.Id == reportId, ct);
            if (report is null) return RemedialTrackParentReportActionResult.NotFound;
            var ctx = await LoadContextAsync(db, report, scope, ct);
            if (ctx is null) return RemedialTrackParentReportActionResult.NotFound;

            if (report.Status == RemedialTrackParentReportStatus.Suppressed)
                return RemedialTrackParentReportActionResult.Ok("التقرير موقوف مسبقًا.");
            if (report.Status == RemedialTrackParentReportStatus.NoParent)
                return RemedialTrackParentReportActionResult.Fail("لا يوجد ولي أمر مرتبط؛ لا حاجة للإيقاف.");

            report.Status = RemedialTrackParentReportStatus.Suppressed;
            if (!await TrySaveAsync(db, ct))
                return RemedialTrackParentReportActionResult.Fail("تغيّرت حالة التقرير للتو. حدّث الصفحة وأعد المحاولة.");

            await LogAsync("RemedialTrack.ParentReportSuppressed", ctx, report, actor,
                $"إيقاف تقرير ولي الأمر {report.Id} (تسجيل {report.EnrollmentId}) — لم يعد ظاهرًا لولي الأمر.");
            return RemedialTrackParentReportActionResult.Ok("✅ تم إيقاف التقرير وإخفاؤه عن ولي الأمر.");
        }

        public async Task<RemedialTrackParentReportActionResult> ToggleAutoSendAsync(
            int publicationId, bool enabled, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var pub = await db.RemedialTrackPublications.FirstOrDefaultAsync(p => p.Id == publicationId, ct);
            if (pub is null || pub.IsDeleted || !scope.Allows(pub.BatchId)) return RemedialTrackParentReportActionResult.NotFound;

            if (pub.AutoSendParentReports == enabled)
                return RemedialTrackParentReportActionResult.Ok(enabled ? "الإرسال التلقائي مفعّل مسبقًا." : "الإرسال التلقائي متوقف مسبقًا.");

            pub.AutoSendParentReports = enabled;
            await db.SaveChangesAsync(ct);

            try
            {
                await _activity.LogAsync("RemedialTrack.AutoSendToggled",
                    $"[RTK pub:{pub.Id}] {(enabled ? "تشغيل" : "إيقاف")} الإرسال التلقائي لتقارير أولياء الأمور (يسري على التقارير القادمة فقط).",
                    actor.UserId, actor.Name, batchId: pub.BatchId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RTK: فشل تسجيل نشاط مفتاح الإرسال التلقائي (أمر {PublicationId})", pub.Id);
            }

            return RemedialTrackParentReportActionResult.Ok(enabled
                ? "✅ تم تشغيل الإرسال التلقائي للتقارير القادمة."
                : "✅ تم إيقاف الإرسال التلقائي؛ ستنتظر التقارير القادمة في الطابور للإرسال اليدوي.");
        }

        // ---------------- مساعدات ----------------

        private static async Task<bool> TrySaveAsync(ApplicationDbContext db, CancellationToken ct)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }

        private async Task LogAsync(string action, ReportContext ctx, RemedialTrackParentReport report, RemedialTrackActor actor, string text)
        {
            try
            {
                await _activity.LogAsync(action, $"[RTK pub:{ctx.PublicationId}] {text}",
                    actor.UserId, actor.Name, studentId: report.StudentId, batchId: ctx.BatchId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RTK: فشل تسجيل نشاط تقرير ولي الأمر {ReportId}", report.Id);
            }
        }
    }
}
