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
    /// RTK-S6: تقارير الطالب وولي الأمر والدفعة، لوحة المتابعة، وفتح المحور التالي.
    /// قراءة فقط عدا ملاحظة التقرير وفتح التالي. كل القراءات AsNoTracking بتجميع في الخادم (لا حلقات ولا Contains على قوائم).
    /// الأوقات UTC (D11) وتُحوَّل للعرض في الـ Views.
    /// </summary>
    public sealed class RemedialTrackReportService : IRemedialTrackReportService
    {
        public const int MinUnlockReasonLength = 10;
        public const int MaxUnlockReasonLength = 300;
        public const int MaxReportNoteLength = 2000;
        public const int MaxBatchReportRows = 1000;

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly IRemedialTrackProgressService _progress;
        private readonly INotificationService _notifications;
        private readonly IAdminActivityLogger _activity;
        private readonly ILogger<RemedialTrackReportService> _logger;

        public RemedialTrackReportService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            IRemedialTrackProgressService progress,
            INotificationService notifications,
            IAdminActivityLogger activity,
            ILogger<RemedialTrackReportService> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _progress = progress;
            _notifications = notifications;
            _activity = activity;
            _logger = logger;
        }

        private DateTime Now => _time.GetUtcNow().UtcDateTime;

        // ======================================================================
        // RTK-S6.1: تقرير الطالب (مالك فقط) — 4 استعلامات
        // ======================================================================

        public async Task<RemedialTrackEnrollmentReportVm?> GetStudentReportAsync(int studentId, int enrollmentId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var report = await BuildReportAsync(db, enrollmentId, studentId, scope: null, includeNote: false, ct);
            if (report is null) return null;

            // الطالب لا يرى تقريرًا لأمر نشر ملغى أو لم يحن وقته (نفس منطق قائمة «خطتي العلاجية»).
            if (report.Status == RemedialTrackEnrollmentStatus.Cancelled || report.PublishAtUtc > Now)
                return null;
            return report;
        }

        // ======================================================================
        // RTK-S6.4: تقرير ولي الأمر + الملاحظة + تقرير الدفعة (أدمن)
        // ======================================================================

        public async Task<RemedialTrackEnrollmentReportVm?> GetParentReportAsync(int enrollmentId, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await BuildReportAsync(db, enrollmentId, ownerStudentId: null, scope, includeNote: true, ct);
        }

        public async Task<RemedialTrackResult> SaveReportNoteAsync(
            int enrollmentId, string? note, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            var clean = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (clean is { Length: > MaxReportNoteLength })
                return RemedialTrackResult.Fail($"⚠️ الملاحظة أطول من {MaxReportNoteLength} حرف.");

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var batchId = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.Id == enrollmentId)
                .Select(e => (int?)e.Publication!.BatchId)
                .FirstOrDefaultAsync(ct);
            if (batchId is null || !scope.Allows(batchId.Value))
                return RemedialTrackResult.Fail("🚫 التسجيل غير متاح لك.");

            var enrollment = await db.RemedialTrackEnrollments.FirstAsync(e => e.Id == enrollmentId, ct);
            enrollment.AdminReportNote = clean;
            db.RemedialTrackEvents.Add(new RemedialTrackEvent
            {
                EnrollmentId = enrollmentId,
                Type = RemedialTrackEventType.ReportNoteSaved,
                Message = "حُفظت ملاحظة تقرير ولي الأمر.",   // نص الملاحظة لا يُكرَّر في السجل
                ActorUserId = Truncate(actor.UserId, 450),
                ActorName = Truncate(actor.Name, 200),
                CreatedAtUtc = Now
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return RemedialTrackResult.Fail("⚠️ تغيّر السجل أثناء الحفظ. أعد تحميل الصفحة وحاول مرة أخرى.");
            }

            return RemedialTrackResult.Ok("✅ تم حفظ الملاحظة.");
        }

        public async Task<RemedialTrackBatchReportVm?> GetBatchReportAsync(int publicationId, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var head = await db.RemedialTrackPublications.AsNoTracking()
                .Where(p => p.Id == publicationId)
                .Select(p => new
                {
                    p.BatchId,
                    TrackTitle = p.Track!.Title,
                    CurriculumTitle = p.Track.Curriculum!.Title,
                    BatchName = p.Batch!.Name,
                    p.Mode,
                    p.PublishAtUtc,
                    TotalAxes = db.RemedialTrackAxes.Count(a => a.TrackId == p.TrackId)
                })
                .FirstOrDefaultAsync(ct);
            if (head is null || !scope.Allows(head.BatchId)) return null;

            var rows = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.PublicationId == publicationId)
                .OrderBy(e => e.Student!.FullName).ThenBy(e => e.Id)
                .Take(MaxBatchReportRows)
                .Select(e => new RemedialTrackBatchReportRowVm
                {
                    EnrollmentId = e.Id,
                    StudentName = e.Student!.FullName,
                    Status = e.Status,
                    PassedAxes = e.AxisProgresses.Count(a => a.Status == RemedialTrackAxisStatus.Passed),
                    FollowUpAxes = e.AxisProgresses.Count(a =>
                        a.Status == RemedialTrackAxisStatus.FailedBlocked || a.Status == RemedialTrackAxisStatus.FailedOpenedByAdmin),
                    LastActivityUtc = db.RemedialTrackEvents
                        .Where(ev => ev.EnrollmentId == e.Id)
                        .Max(ev => (DateTime?)ev.CreatedAtUtc)
                })
                .ToListAsync(ct);

            return new RemedialTrackBatchReportVm
            {
                PublicationId = publicationId,
                TrackTitle = head.TrackTitle,
                CurriculumTitle = head.CurriculumTitle,
                BatchName = head.BatchName,
                Mode = head.Mode,
                PublishAtUtc = head.PublishAtUtc,
                TotalAxes = head.TotalAxes,
                IssuedAtUtc = Now,
                Rows = rows
            };
        }

        // ======================================================================
        // RTK-S6.2: لوحة المتابعة — حالات التسجيل (+فحص النطاق) / رسم المحاور / من لم يجتز / العدّ / الصفحة
        // ======================================================================

        public async Task<RemedialTrackDashboardVm?> GetDashboardAsync(
            int publicationId, RemedialTrackDashboardFilter filter, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var search = filter.Q?.Trim();
            if (search is { Length: > RemedialTrackDashboardFilter.MaxSearchLength })
                search = search[..RemedialTrackDashboardFilter.MaxSearchLength];
            if (string.IsNullOrEmpty(search)) search = null;

            // 1) حالات التسجيل + الدفعة (BatchId ثابت لأمر النشر فتتطابق المجموعات مع الحالات) — يخدم KPIs وفحص النطاق معًا.
            var statusRows = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.PublicationId == publicationId)
                .GroupBy(e => new { e.Status, BatchId = e.Publication!.BatchId })
                .Select(g => new { g.Key.Status, g.Key.BatchId, Count = g.Count() })
                .ToListAsync(ct);
            if (statusRows.Count == 0 || !scope.Allows(statusRows[0].BatchId)) return null;

            int CountOf(RemedialTrackEnrollmentStatus s) => statusRows.Where(r => r.Status == s).Sum(r => r.Count);
            var kpis = new RemedialTrackDashboardKpisVm
            {
                Total = statusRows.Sum(r => r.Count),
                NotStarted = CountOf(RemedialTrackEnrollmentStatus.NotStarted),
                InProgress = CountOf(RemedialTrackEnrollmentStatus.InProgress),
                Completed = CountOf(RemedialTrackEnrollmentStatus.Completed),
                CompletedWithFailures = CountOf(RemedialTrackEnrollmentStatus.CompletedWithFailures),
                Cancelled = CountOf(RemedialTrackEnrollmentStatus.Cancelled)
            };

            // 2) رسم نتائج المحاور (GROUP BY في SQL؛ التسجيلات الملغاة مستثناة)
            var chart = await db.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(ap => ap.Enrollment!.PublicationId == publicationId && ap.Enrollment.Status != RemedialTrackEnrollmentStatus.Cancelled)
                .GroupBy(ap => new { ap.Order, Title = ap.Axis!.TitleOverride ?? ap.Axis.Section!.Title })
                .Select(g => new RemedialTrackAxisChartRowVm
                {
                    Order = g.Key.Order,
                    Title = g.Key.Title,
                    Attempted = g.Count(x => x.Exam101Percent != null),
                    PassedFirstTime = g.Count(x => x.Status == RemedialTrackAxisStatus.Passed && x.Exam102Percent == null && x.Exam101Percent != null),
                    PassedAfterRewatch = g.Count(x => x.Status == RemedialTrackAxisStatus.Passed && x.Exam102Percent != null),
                    NotPassed = g.Count(x => x.Status == RemedialTrackAxisStatus.FailedBlocked || x.Status == RemedialTrackAxisStatus.FailedOpenedByAdmin)
                })
                .OrderBy(r => r.Order)
                .ToListAsync(ct);
            var lastOrder = chart.Count == 0 ? 0 : chart.Max(r => r.Order);   // «محجوب بانتظار الإدارة» = FailedBlocked قبل آخر محور (آخر محور لا يُفتح بعده شيء)

            // 3) من لم يجتز (محجوبون أولًا ثم من نُقل بقرار الإدارة) — ومنه عدّاد «محجوب بانتظار الإدارة»
            var failed = await (
                    from ap in db.RemedialTrackAxisProgresses.AsNoTracking()
                    where ap.Enrollment!.PublicationId == publicationId
                          && ap.Enrollment.Status != RemedialTrackEnrollmentStatus.Cancelled
                          && (ap.Status == RemedialTrackAxisStatus.FailedBlocked || ap.Status == RemedialTrackAxisStatus.FailedOpenedByAdmin)
                    orderby ap.Status, ap.Enrollment.Student!.FullName, ap.Order
                    select new RemedialTrackNotPassedRowVm
                    {
                        EnrollmentId = ap.EnrollmentId,
                        StudentName = ap.Enrollment.Student!.FullName,
                        AxisTitle = ap.Axis!.TitleOverride ?? ap.Axis.Section!.Title,
                        Status = ap.Status
                    })
                .Take(RemedialTrackDashboardVm.MaxNotPassedListed + 1)
                .ToListAsync(ct);
            var failedTruncated = failed.Count > RemedialTrackDashboardVm.MaxNotPassedListed;
            if (failedTruncated) failed.RemoveAt(failed.Count - 1);
            kpis.BlockedAwaitingAdmin = chart.Count == 0 ? 0 : await CountBlockedAsync(db, publicationId, lastOrder, ct);

            // 4-5) الجدول المرقّم من الخادم (عدّ + صفحة)
            var q = db.RemedialTrackEnrollments.AsNoTracking().Where(e => e.PublicationId == publicationId);
            if (filter.Status.HasValue)
            {
                var st = filter.Status.Value;
                q = q.Where(e => e.Status == st);
            }
            if (filter.BlockedOnly)
                q = q.Where(e => e.AxisProgresses.Any(a => a.Status == RemedialTrackAxisStatus.FailedBlocked && a.Order < lastOrder));
            if (search is not null)
                q = q.Where(e => e.Student!.FullName.Contains(search));

            var total = await q.CountAsync(ct);
            var totalPages = total <= 0 ? 1 : (int)Math.Ceiling(total / (double)RemedialTrackDashboardFilter.PageSize);
            var page = Math.Clamp(filter.Page, 1, totalPages);

            var students = await q
                .OrderBy(e => e.Student!.FullName).ThenBy(e => e.Id)
                .Skip((page - 1) * RemedialTrackDashboardFilter.PageSize)
                .Take(RemedialTrackDashboardFilter.PageSize)
                .Select(e => new RemedialTrackDashboardStudentRowVm
                {
                    EnrollmentId = e.Id,
                    StudentId = e.StudentId,
                    StudentName = e.Student!.FullName,
                    Status = e.Status,
                    CurrentAxisTitle = e.AxisProgresses
                        .Where(a => a.AxisId == e.CurrentAxisId)
                        .Select(a => a.Axis!.TitleOverride ?? a.Axis.Section!.Title).FirstOrDefault(),
                    CurrentAxisOrder = e.AxisProgresses
                        .Where(a => a.AxisId == e.CurrentAxisId)
                        .Select(a => (int?)a.Order).FirstOrDefault(),
                    CurrentRound = e.AxisProgresses
                        .Where(a => a.AxisId == e.CurrentAxisId)
                        .Select(a => (int?)a.Round).FirstOrDefault(),
                    LastExamNumber = db.RemedialTrackExamAttempts
                        .Where(t => t.AxisProgress!.EnrollmentId == e.Id && t.Status != RemedialTrackAttemptStatus.InProgress)
                        .OrderByDescending(t => t.SubmittedAtUtc ?? t.ExpiresAtUtc).ThenByDescending(t => t.Id)
                        .Select(t => (RemedialTrackExamNumber?)t.ExamNumber).FirstOrDefault(),
                    LastScorePercent = db.RemedialTrackExamAttempts
                        .Where(t => t.AxisProgress!.EnrollmentId == e.Id && t.Status != RemedialTrackAttemptStatus.InProgress)
                        .OrderByDescending(t => t.SubmittedAtUtc ?? t.ExpiresAtUtc).ThenByDescending(t => t.Id)
                        .Select(t => (double?)t.ScorePercent).FirstOrDefault(),
                    LastActivityUtc = db.RemedialTrackEvents
                        .Where(ev => ev.EnrollmentId == e.Id)
                        .Max(ev => (DateTime?)ev.CreatedAtUtc),
                    BlockedAxisProgressId = e.AxisProgresses
                        .Where(a => a.Status == RemedialTrackAxisStatus.FailedBlocked && a.Order < lastOrder)
                        .OrderBy(a => a.Order).Select(a => (int?)a.Id).FirstOrDefault(),
                    BlockedAxisTitle = e.AxisProgresses
                        .Where(a => a.Status == RemedialTrackAxisStatus.FailedBlocked && a.Order < lastOrder)
                        .OrderBy(a => a.Order).Select(a => a.Axis!.TitleOverride ?? a.Axis.Section!.Title).FirstOrDefault()
                })
                .ToListAsync(ct);

            return new RemedialTrackDashboardVm
            {
                Kpis = kpis,
                AxisChart = chart,
                NotPassed = failed,
                NotPassedTruncated = failedTruncated,
                Students = students,
                Filter = new RemedialTrackDashboardFilter { Status = filter.Status, BlockedOnly = filter.BlockedOnly, Q = search, Page = page },
                Page = page,
                Total = total
            };
        }

        // عدد الطلاب المحجوبين فعليًا (FailedBlocked قبل آخر محور). يُحسب من نفس الصفوف لتجنّب استعلام إضافي حين القائمة غير مبتورة.
        private static Task<int> CountBlockedAsync(ApplicationDbContext db, int publicationId, int lastOrder, CancellationToken ct)
            => db.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(ap => ap.Enrollment!.PublicationId == publicationId
                             && ap.Enrollment.Status != RemedialTrackEnrollmentStatus.Cancelled
                             && ap.Status == RemedialTrackAxisStatus.FailedBlocked
                             && ap.Order < lastOrder)
                .Select(ap => ap.EnrollmentId)
                .Distinct()
                .CountAsync(ct);

        // ======================================================================
        // RTK-S6.3: فتح المحور التالي (إدارة)
        // ======================================================================

        public async Task<RemedialTrackResult> UnlockNextAxisAsync(
            int enrollmentId, int axisProgressId, string? reason, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default)
        {
            var cleanReason = reason?.Trim();
            if (string.IsNullOrEmpty(cleanReason) || cleanReason.Length < MinUnlockReasonLength || cleanReason.Length > MaxUnlockReasonLength)
                return RemedialTrackResult.Fail($"⚠️ سبب فتح المحور التالي إلزامي ({MinUnlockReasonLength}–{MaxUnlockReasonLength} حرف).");

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            // انتماء المحور للتسجيل + نطاق الدفعة + حالة أمر النشر — في استعلام واحد، ورسالة موحّدة لا تكشف أي تفصيل (D14)
            var info = await db.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(a => a.Id == axisProgressId && a.EnrollmentId == enrollmentId)
                .Select(a => new
                {
                    a.Status,
                    a.Order,
                    AxisTitle = a.Axis!.TitleOverride ?? a.Axis.Section!.Title,
                    EnrollmentStatus = a.Enrollment!.Status,
                    a.Enrollment.StudentId,
                    a.Enrollment.PublicationId,
                    PublicationStatus = a.Enrollment.Publication!.Status,
                    BatchId = a.Enrollment.Publication.BatchId
                })
                .FirstOrDefaultAsync(ct);
            if (info is null || !scope.Allows(info.BatchId))
                return RemedialTrackResult.Fail("🚫 التسجيل غير متاح لك.");

            if (info.PublicationStatus != RemedialTrackPublicationStatus.Active || info.EnrollmentStatus == RemedialTrackEnrollmentStatus.Cancelled)
                return RemedialTrackResult.Fail("⚠️ أمر النشر غير نشط أو التسجيل ملغى.");
            if (info.Status != RemedialTrackAxisStatus.FailedBlocked)
                return RemedialTrackResult.Fail("⚠️ فتح المحور التالي مسموح فقط لمحور حُجب بعد عدم الاجتياز في الاختبارين.");

            var nextTitle = await db.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(a => a.EnrollmentId == enrollmentId && a.Order > info.Order)
                .OrderBy(a => a.Order)
                .Select(a => a.Axis!.TitleOverride ?? a.Axis.Section!.Title)
                .FirstOrDefaultAsync(ct);
            if (nextTitle is null)
                return RemedialTrackResult.Fail("⚠️ هذا آخر محور في الخطة؛ لا يوجد محور تالٍ لفتحه.");

            var transition = await _progress.AdminOpenNextAsync(axisProgressId, cleanReason, actor, ct);
            if (transition.Error is not null)
                return RemedialTrackResult.Fail("⚠️ " + transition.Error);
            if (!transition.Applied)
                return RemedialTrackResult.Fail("ℹ️ سبق فتح المحور التالي لهذا الطالب.");

            // بعد نجاح الـ Transaction: الإشعار وسجل النشاط — فشلهما لا يُبطل الفتح
            try
            {
                await _notifications.SendToStudentAsync(
                    info.StudentId,
                    $"📘 فتحت الإدارة المحور التالي: {nextTitle}.",
                    NotificationCategory.Remedial,
                    $"/Students/RemedialTrack/Open?enrollmentId={enrollmentId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RTK: فشل إشعار فتح المحور التالي (تسجيل {EnrollmentId})", enrollmentId);
            }

            try
            {
                await _activity.LogAsync(
                    "RemedialTrack.UnlockNext",
                    $"فتح المحور التالي (بعد «{info.AxisTitle}») للتسجيل {enrollmentId} في أمر النشر {info.PublicationId}. السبب: {cleanReason}",
                    actor.UserId, actor.Name, studentId: info.StudentId, batchId: info.BatchId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RTK: فشل تسجيل نشاط الأدمن لفتح المحور التالي (تسجيل {EnrollmentId})", enrollmentId);
            }

            return RemedialTrackResult.Ok($"✅ فُتح المحور التالي: {nextTitle}.", new { nextTitle });
        }

        // ======================================================================
        // البناء المشترك لتقرير تسجيل واحد — 4 استعلامات (ترويسة، محاور، تقدّم فيديو مجمّع، محاولات)
        // ======================================================================

        private async Task<RemedialTrackEnrollmentReportVm?> BuildReportAsync(
            ApplicationDbContext db, int enrollmentId, int? ownerStudentId, RemedialTrackBatchScope? scope, bool includeNote, CancellationToken ct)
        {
            var headQuery = db.RemedialTrackEnrollments.AsNoTracking().Where(e => e.Id == enrollmentId);
            if (ownerStudentId.HasValue)
            {
                var owner = ownerStudentId.Value;
                headQuery = headQuery.Where(e => e.StudentId == owner);
            }

            var head = await headQuery
                .Select(e => new
                {
                    e.Id,
                    StudentName = e.Student!.FullName,
                    BatchId = e.Publication!.BatchId,
                    BatchName = e.Publication.Batch!.Name,
                    TrackTitle = e.Publication.Track!.Title,
                    CurriculumTitle = e.Publication.Track.Curriculum!.Title,
                    PassPercent = e.Publication.Track.PassPercent,
                    e.Publication.Mode,
                    e.Publication.PublishAtUtc,
                    e.Status,
                    e.StartedAtUtc,
                    e.CompletedAtUtc,
                    e.AdminReportNote
                })
                .FirstOrDefaultAsync(ct);
            if (head is null) return null;
            if (scope is not null && !scope.Allows(head.BatchId)) return null;

            var axes = await db.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(a => a.EnrollmentId == enrollmentId)
                .OrderBy(a => a.Order)
                .Select(a => new
                {
                    a.Id,
                    a.Order,
                    Title = a.Axis!.TitleOverride ?? a.Axis.Section!.Title,
                    a.Status,
                    a.Round,
                    a.Exam101Percent,
                    a.Exam102Percent,
                    a.PassedAtUtc,
                    a.AdminOpenedAtUtc,
                    VideoTotal = db.RemedialTrackVideos.Count(v => v.AxisId == a.AxisId && v.IsActive)
                })
                .ToListAsync(ct);

            var videoDone = await db.RemedialTrackVideoProgresses.AsNoTracking()
                .Where(v => v.AxisProgress!.EnrollmentId == enrollmentId && v.IsCompleted)
                .GroupBy(v => new { v.AxisProgressId, v.Round })
                .Select(g => new { g.Key.AxisProgressId, g.Key.Round, Count = g.Count() })
                .ToListAsync(ct);

            var attempts = await db.RemedialTrackExamAttempts.AsNoTracking()
                .Where(t => t.AxisProgress!.EnrollmentId == enrollmentId && t.Status != RemedialTrackAttemptStatus.InProgress)
                .Select(t => new { t.AxisProgressId, t.ExamNumber, SubmittedAtUtc = t.SubmittedAtUtc ?? t.ExpiresAtUtc })
                .ToListAsync(ct);

            var rows = axes.Select(a =>
            {
                int Done(int round) => videoDone.Where(v => v.AxisProgressId == a.Id && v.Round == round).Sum(v => v.Count);
                DateTime? SubmittedAt(RemedialTrackExamNumber n) => attempts
                    .Where(t => t.AxisProgressId == a.Id && t.ExamNumber == n)
                    .Select(t => (DateTime?)t.SubmittedAtUtc).FirstOrDefault();

                var round2 = Done(2);
                return new RemedialTrackReportAxisVm
                {
                    Order = a.Order,
                    Title = a.Title,
                    Status = a.Status,
                    Outcome = OutcomeOf(a.Status),
                    Round = a.Round,
                    VideoTotal = a.VideoTotal,
                    VideosDoneRound1 = Done(1),
                    VideosDoneRound2 = round2,
                    HasRound2 = a.Round >= 2 || round2 > 0,
                    Exam101Percent = a.Exam101Percent,
                    Exam101SubmittedAtUtc = SubmittedAt(RemedialTrackExamNumber.Exam101),
                    Exam102Percent = a.Exam102Percent,
                    Exam102SubmittedAtUtc = SubmittedAt(RemedialTrackExamNumber.Exam102),
                    PassedAtUtc = a.PassedAtUtc,
                    AdminOpenedAtUtc = a.AdminOpenedAtUtc
                };
            }).ToList();

            return new RemedialTrackEnrollmentReportVm
            {
                EnrollmentId = head.Id,
                StudentName = head.StudentName,
                BatchName = head.BatchName,
                TrackTitle = head.TrackTitle,
                CurriculumTitle = head.CurriculumTitle,
                Mode = head.Mode,
                PublishAtUtc = head.PublishAtUtc,
                Status = head.Status,
                StartedAtUtc = head.StartedAtUtc,
                CompletedAtUtc = head.CompletedAtUtc,
                PassPercent = head.PassPercent,
                IssuedAtUtc = Now,
                AdminNote = includeNote ? head.AdminReportNote : null,
                Axes = rows
            };
        }

        private static RemedialTrackReportAxisOutcome OutcomeOf(RemedialTrackAxisStatus status) => status switch
        {
            RemedialTrackAxisStatus.Passed => RemedialTrackReportAxisOutcome.Passed,
            RemedialTrackAxisStatus.FailedBlocked => RemedialTrackReportAxisOutcome.NeedsFollowUp,
            RemedialTrackAxisStatus.FailedOpenedByAdmin => RemedialTrackReportAxisOutcome.MovedByAdmin,
            RemedialTrackAxisStatus.Locked => RemedialTrackReportAxisOutcome.NotReached,
            _ => RemedialTrackReportAxisOutcome.InProgress
        };

        private static string Truncate(string? value, int max)
            => string.IsNullOrEmpty(value) ? string.Empty : (value.Length <= max ? value : value[..max]);
    }
}
