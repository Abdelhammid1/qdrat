using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>نتيجة حساب نبضة واحدة (دالة نقية — D4).</summary>
    public readonly record struct RemedialTrackPingComputation(
        double WatchedSeconds,
        double DurationSeconds,
        double RequiredSeconds,
        bool Ended,
        bool Completed);

    /// <summary>
    /// RTK-S4: ما يراه الطالب (القائمة، الخطة، المحور) + تسجيل تقدّم الفيديو بزمن الخادم (D4/D5).
    /// RTK-S5: انتقالات آلة الحالة (اكتمال الفيديوهات، تسليم 101/102، فتح الأدمن للتالي) داخل Transaction + RowVersion.
    /// </summary>
    public sealed class RemedialTrackProgressService : IRemedialTrackProgressService
    {
        public const double MaxCreditPerPingSeconds = 20;   // نبضة كل 15ث + هامش
        private const double DurationCeilingFactor = 1.05;  // سقف منطقي للمشاهدة المحتسبة

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly ITimeZoneService _tz;
        private readonly ILogger<RemedialTrackProgressService> _logger;

        public RemedialTrackProgressService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            ITimeZoneService tz,
            ILogger<RemedialTrackProgressService> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _tz = tz;
            _logger = logger;
        }

        // ═════════════════ الحساب النقي للنبضة ═════════════════

        /// <summary>
        /// الرصيد الزمني يُمنح عن الفترة السابقة فقط إن كانت آخر حالة مسجّلة «playing» (سقف 20ث)،
        /// ولا رصيد لأول نبضة. المدة المُدخلة من الأدمن تتقدّم على مدة العميل؛ ومدة العميل لا تنخفض أبدًا بعد حفظها.
        /// </summary>
        public static RemedialTrackPingComputation ComputePing(
            double previousWatched, bool previousEnded, string? lastState, DateTime? lastPingAtUtc,
            DateTime nowUtc, string state, RemedialTrackVideoProvider provider,
            int? adminDuration, int? storedDuration, double clientDuration, int minWatchPercent)
        {
            double clientSafe = double.IsNaN(clientDuration) || double.IsInfinity(clientDuration) ? 0 : clientDuration;
            double duration = adminDuration.HasValue && adminDuration.Value > 0
                ? adminDuration.Value
                : Math.Max(storedDuration ?? 0, Math.Clamp(clientSafe, 5, 4 * 3600));

            double credit = 0;
            if (lastState == "playing" && lastPingAtUtc.HasValue)
                credit = Math.Clamp((nowUtc - lastPingAtUtc.Value).TotalSeconds, 0, MaxCreditPerPingSeconds);

            var ended = previousEnded || state == "ended";
            if (provider == RemedialTrackVideoProvider.Other && state == "manual-done") ended = true;   // D5

            var watched = Math.Min(previousWatched + credit, duration * DurationCeilingFactor);
            var required = Math.Ceiling(duration * minWatchPercent / 100.0);
            return new RemedialTrackPingComputation(watched, duration, required, ended, ended && watched >= required);
        }

        // ═════════════════ RTK-S4.1: القائمة ═════════════════

        public async Task<StudentRemedialTrackIndexVm> GetMyPlansAsync(int studentId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var rows = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.StudentId == studentId
                            && e.Status != RemedialTrackEnrollmentStatus.Cancelled
                            && e.Publication!.Status == RemedialTrackPublicationStatus.Active
                            && !e.Publication.IsDeleted
                            && e.Publication.PublishAtUtc <= now)
                .OrderByDescending(e => e.Publication!.PublishAtUtc)
                .Select(e => new
                {
                    e.Id,
                    TrackTitle = e.Publication!.Track!.Title,
                    Curriculum = e.Publication.Track.Curriculum!.Title,
                    TrackCreatedAtUtc = e.Publication.Track.CreatedAtUtc,
                    e.Publication.PublishAtUtc,
                    e.Publication.Mode,
                    e.Status,
                    Total = e.AxisProgresses.Count(),
                    Passed = e.AxisProgresses.Count(a => a.Status == RemedialTrackAxisStatus.Passed)
                })
                .Take(200)
                .ToListAsync(ct);

            // RTK-S13: ملاحق معلّقة للطالب كله في استعلام واحد (بلا IN): فعّالة، غير مكتملة، محورها مفتوح، الأمر نشط وغير محذوف
            var pendingRows = await db.RemedialTrackAddendumProgresses.AsNoTracking()
                .Where(p => p.Enrollment!.StudentId == studentId
                            && p.CompletedAtUtc == null
                            && p.Addendum!.IsActive
                            && p.Enrollment.Status != RemedialTrackEnrollmentStatus.Cancelled
                            && p.Enrollment.Publication!.Status == RemedialTrackPublicationStatus.Active
                            && !p.Enrollment.Publication.IsDeleted
                            && p.Enrollment.AxisProgresses.Any(a => a.AxisId == p.Addendum.AxisId && a.Status != RemedialTrackAxisStatus.Locked))
                .OrderBy(p => p.Addendum!.CreatedAtUtc)
                .Select(p => new
                {
                    p.EnrollmentId,
                    p.AddendumId,
                    p.Addendum!.Title,
                    AxisTitle = p.Addendum.Axis!.TitleOverride ?? p.Addendum.Axis.Section!.Title
                })
                .Take(500)
                .ToListAsync(ct);
            var pendingByEnrollment = pendingRows.ToLookup(x => x.EnrollmentId);

            return new StudentRemedialTrackIndexVm
            {
                Items = rows.Select(r => new StudentRemedialTrackListItemVm
                {
                    PendingAddenda = pendingByEnrollment[r.Id]
                        .Select(x => new StudentRemedialTrackPendingAddendumVm { AddendumId = x.AddendumId, Title = x.Title, AxisTitle = x.AxisTitle })
                        .ToList(),
                    EnrollmentId = r.Id,
                    TrackTitle = r.TrackTitle,
                    CurriculumName = r.Curriculum ?? string.Empty,
                    TrackCreatedAtLocal = _tz.ConvertToSaudi(r.TrackCreatedAtUtc),
                    PublishAtLocal = _tz.ConvertToSaudi(r.PublishAtUtc),
                    Mode = r.Mode,
                    Status = r.Status,
                    TotalAxes = r.Total,
                    PassedAxes = r.Passed
                }).ToList()
            };
        }

        // ═════════════════ RTK-S4.3: صفحة الخطة ═════════════════

        public async Task<StudentRemedialTrackPlanVm?> GetPlanAsync(int studentId, int enrollmentId, CancellationToken ct = default)
        {
            await OpenPendingAxesAsync(studentId, enrollmentId, ct);   // v2: فتح المحور الذي أنهى الطالب سابقَه (يعالج جدولة الأيام القديمة)

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var head = await db.RemedialTrackEnrollments.AsNoTracking()
                .Where(e => e.Id == enrollmentId && e.StudentId == studentId && !e.Publication!.IsDeleted)
                .Select(e => new
                {
                    e.Id,
                    e.Status,
                    e.Publication!.PublishAtUtc,
                    e.Publication.Mode,
                    TrackTitle = e.Publication.Track!.Title,
                    Description = e.Publication.Track.Description,
                    Curriculum = e.Publication.Track.Curriculum!.Title
                })
                .FirstOrDefaultAsync(ct);
            if (head is null) return null;

            var axes = await db.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(a => a.EnrollmentId == enrollmentId)
                .OrderBy(a => a.Order)
                .Select(a => new StudentRemedialTrackAxisItemVm
                {
                    AxisProgressId = a.Id,
                    AxisId = a.AxisId,
                    Order = a.Order,
                    Title = a.Axis!.TitleOverride ?? a.Axis.Section!.Title,
                    Status = a.Status,
                    Round = a.Round,
                    VideoCount = a.Axis.Videos.Count(v => v.IsActive),
                    Exam101Percent = a.Exam101Percent,
                    Exam102Percent = a.Exam102Percent
                })
                .ToListAsync(ct);

            // RTK-S13: «مطلوب إضافي» — استعلام واحد؛ تظهر حتى لو المحور Passed، ولا تظهر لمحور Locked (D31). لا تمنع التقدّم (D29)
            var openAxisIds = new HashSet<int>(axes.Where(a => !a.IsLocked).Select(a => a.AxisId));
            var cardRows = await db.RemedialTrackAddendumProgresses.AsNoTracking()
                .Where(p => p.EnrollmentId == enrollmentId && p.Addendum!.IsActive)
                .OrderBy(p => p.Addendum!.CreatedAtUtc).ThenBy(p => p.AddendumId)
                .Select(p => new
                {
                    p.AddendumId,
                    p.Addendum!.AxisId,
                    p.Addendum.Title,
                    p.Addendum.Reason,
                    HasExam = p.Addendum.ExamModelId != null,
                    p.WatchedSeconds,
                    p.VideoCompleted,
                    p.CompletedAtUtc,
                    p.AttemptsCount
                })
                .ToListAsync(ct);

            var addenda = cardRows
                .Where(r => openAxisIds.Contains(r.AxisId))
                .Select(r => new StudentRemedialTrackAddendumCardVm
                {
                    AddendumId = r.AddendumId,
                    AxisId = r.AxisId,
                    Title = r.Title,
                    Reason = r.Reason,
                    HasExam = r.HasExam,
                    State = RemedialTrackAddendumRules.StateOf(r.WatchedSeconds, r.VideoCompleted, r.AttemptsCount, r.CompletedAtUtc.HasValue)
                })
                .ToList();

            return new StudentRemedialTrackPlanVm
            {
                EnrollmentId = head.Id,
                TrackTitle = head.TrackTitle,
                TrackDescription = head.Description,
                CurriculumName = head.Curriculum ?? string.Empty,
                Mode = head.Mode,
                Status = head.Status,
                Axes = axes,
                Addenda = addenda
            };
        }

        // ═════════════════ RTK-S4.3: صفحة المحور ═════════════════

        public async Task<StudentRemedialTrackAxisVm?> GetAxisAsync(int studentId, int enrollmentId, int axisProgressId, CancellationToken ct = default)
        {
            await OpenPendingAxesAsync(studentId, enrollmentId, ct);

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var ap = await db.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(a => a.Id == axisProgressId && a.EnrollmentId == enrollmentId && a.Enrollment!.StudentId == studentId
                            && !a.Enrollment.Publication!.IsDeleted)
                .Select(a => new
                {
                    a.Id,
                    a.AxisId,
                    a.Order,
                    a.Status,
                    a.Round,
                    AxisTitle = a.Axis!.TitleOverride ?? a.Axis.Section!.Title,
                    TrackTitle = a.Enrollment!.Publication!.Track!.Title,
                    MinWatch = a.Enrollment.Publication.Track.MinWatchPercent,
                    ExamMinutes = a.Axis.ExamDurationMinutes,
                    ReviewEnabled = a.Enrollment.VideoReviewEnabled,
                    ReviewUntilUtc = a.Enrollment.VideoReviewUntilUtc,
                    EnrollmentActive = a.Enrollment.Status != RemedialTrackEnrollmentStatus.Cancelled
                                       && a.Enrollment.Publication.Status == RemedialTrackPublicationStatus.Active
                })
                .FirstOrDefaultAsync(ct);

            if (ap is null || ap.Status == RemedialTrackAxisStatus.Locked) return null;   // Locked لا يُراجع أبدًا (D24)

            // العلامة المائية: استعلام صغير منفصل حتى لا يتأثر الاستعلام الرئيسي
            var who = await db.Students.AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => new { s.FullName, s.NationalID })
                .FirstOrDefaultAsync(ct);
            var studentName = who?.FullName ?? string.Empty;
            var nationalId = (who?.NationalID ?? string.Empty).Trim();

            var canWatch = (ap.Status == RemedialTrackAxisStatus.Videos && ap.Round == 1)
                           || (ap.Status == RemedialTrackAxisStatus.Rewatch && ap.Round == 2);

            // محور انتهى (اجتاز / لم يجتز): لا فيديوهات ولا روابط؛ إعادة الفتح ممنوعة بعد الانتقال
            var videosClosed = ap.Status is RemedialTrackAxisStatus.Passed
                or RemedialTrackAxisStatus.FailedBlocked or RemedialTrackAxisStatus.FailedOpenedByAdmin;

            // RTK v2/D24: وضع المراجعة — للقراءة فقط، حساب الخادم لحظة الطلب (لا JS)، لمحور منتهٍ فقط (Locked أُخرج أعلاه)
            var reviewMode = videosClosed && ap.EnrollmentActive
                             && RemedialTrackStateMachine.IsVideoReviewOpen(ap.ReviewEnabled, ap.ReviewUntilUtc, _time.GetUtcNow().UtcDateTime);

            if (canWatch)
                await EnsureVideoProgressRowsAsync(db, ap.Id, ap.AxisId, ap.Round, ct);   // لا كتابة في مسار المراجعة

            var rows = await db.RemedialTrackVideoProgresses.AsNoTracking()
                .Where(v => (!videosClosed || reviewMode) && v.AxisProgressId == ap.Id && v.Round == ap.Round && v.Video!.IsActive)
                .OrderBy(v => v.VideoOrder)
                .Select(v => new
                {
                    v.Id,
                    v.VideoOrder,
                    v.Video!.Title,
                    v.Video.Provider,
                    v.Video.ExternalId,
                    v.Video.Url,
                    Duration = v.Video.DurationSeconds ?? v.DurationSeconds,
                    v.IsCompleted,
                    v.WatchedSeconds
                })
                .ToListAsync(ct);

            var videos = new List<StudentRemedialTrackVideoItemVm>(rows.Count);
            var previousDone = true;
            foreach (var r in rows)
            {
                videos.Add(new StudentRemedialTrackVideoItemVm
                {
                    VideoProgressId = r.Id,
                    Order = r.VideoOrder,
                    Title = r.Title,
                    Provider = r.Provider,
                    // لا يغادر الخادمَ معرّف/رابط فيديو خارج الجولة الحالية (انتظار اختبار): لا إعادة مشاهدة
                    // (ما عدا وضع المراجعة D24: الأدمن فتحها صراحةً للقراءة فقط)
                    ExternalId = canWatch || reviewMode ? r.ExternalId : null,
                    Url = canWatch || reviewMode ? r.Url : string.Empty,
                    EmbedUrl = canWatch || reviewMode ? RemedialTrackVideoUrlParser.BuildEmbedUrl(r.Provider, r.ExternalId) : null,
                    DurationSeconds = r.Duration,
                    IsCompleted = r.IsCompleted,
                    IsUnlocked = previousDone || reviewMode,
                    WatchedSeconds = (int)Math.Floor(r.WatchedSeconds),
                    RequiredSeconds = r.Duration.HasValue
                        ? (int)Math.Ceiling(r.Duration.Value * ap.MinWatch / 100.0)
                        : 0
                });
                previousDone = previousDone && r.IsCompleted;
            }

            var pendingExam = ap.Status switch
            {
                RemedialTrackAxisStatus.AwaitingExam101 => RemedialTrackExamNumber.Exam101,
                RemedialTrackAxisStatus.AwaitingExam102 => RemedialTrackExamNumber.Exam102,
                _ => (RemedialTrackExamNumber?)null
            };

            // RTK-S5: محاولات المحور (استعلام واحد صغير)
            var attempts = await db.RemedialTrackExamAttempts.AsNoTracking()
                .Where(x => x.AxisProgressId == ap.Id && x.AddendumId == null)   // RTK-S13: محاولات الملحق لها صفحتها
                .OrderBy(x => x.ExamNumber)
                .Select(x => new StudentRemedialTrackAttemptItemVm
                {
                    AttemptId = x.Id,
                    ExamNumber = x.ExamNumber,
                    Status = x.Status,
                    ScorePercent = x.ScorePercent,
                    IsPassed = x.IsPassed
                })
                .ToListAsync(ct);

            return new StudentRemedialTrackAxisVm
            {
                EnrollmentId = enrollmentId,
                AxisProgressId = ap.Id,
                TrackTitle = ap.TrackTitle,
                AxisTitle = ap.AxisTitle,
                AxisOrder = ap.Order,
                Status = ap.Status,
                Round = ap.Round,
                CanWatch = canWatch,
                VideosClosed = videosClosed && !reviewMode,
                IsReviewMode = reviewMode,
                ReviewUntilLocal = reviewMode && ap.ReviewUntilUtc.HasValue ? _tz.ConvertToSaudi(ap.ReviewUntilUtc.Value) : null,
                WatermarkText = studentName,
                WatermarkIdText = nationalId,
                AllVideosDone = videos.Count > 0 && videos.All(v => v.IsCompleted),
                ExamReady = ap.Status is RemedialTrackAxisStatus.AwaitingExam101 or RemedialTrackAxisStatus.AwaitingExam102,
                PendingExam = pendingExam,
                ExamDurationMinutes = ap.ExamMinutes,
                InProgressAttemptId = pendingExam.HasValue
                    ? attempts.Where(x => x.ExamNumber == pendingExam.Value && !x.IsClosed).Select(x => (int?)x.AttemptId).FirstOrDefault()
                    : null,
                Attempts = attempts,
                Videos = videos
            };
        }

        // إنشاء كسول لصفوف تقدّم فيديوهات الجولة دفعة واحدة؛ الفريد (AxisProgressId, VideoId, Round) يحمي من التزامن
        private async Task EnsureVideoProgressRowsAsync(ApplicationDbContext db, int axisProgressId, int axisId, int round, CancellationToken ct)
        {
            var videos = await db.RemedialTrackVideos.AsNoTracking()
                .Where(v => v.AxisId == axisId && v.IsActive)
                .OrderBy(v => v.Order)
                .Select(v => new { v.Id, v.Order, v.DurationSeconds })
                .ToListAsync(ct);
            if (videos.Count == 0) return;

            var existing = await db.RemedialTrackVideoProgresses.AsNoTracking()
                .Where(v => v.AxisProgressId == axisProgressId && v.Round == round)
                .Select(v => v.VideoId)
                .ToListAsync(ct);
            var have = new HashSet<int>(existing);

            var missing = videos.Where(v => !have.Contains(v.Id)).ToList();
            if (missing.Count == 0) return;

            foreach (var v in missing)
            {
                db.RemedialTrackVideoProgresses.Add(new RemedialTrackVideoProgress
                {
                    AxisProgressId = axisProgressId,
                    VideoId = v.Id,
                    VideoOrder = v.Order,
                    Round = round,
                    DurationSeconds = v.DurationSeconds
                });
            }

            try
            {
                await db.SaveChangesAsync(ct);   // SaveChanges واحد خارج أي Loop
            }
            catch (DbUpdateException ex)
            {
                // أنشأها طلب متزامن — يكفي أن الصفوف موجودة
                _logger.LogInformation(ex, "RTK video-progress rows already created concurrently (axisProgress {AxisProgressId}, round {Round})", axisProgressId, round);
                db.ChangeTracker.Clear();
            }
        }

        // ═════════════════ RTK-S4.4: النبضة ═════════════════

        public async Task<RemedialTrackPingResult> RecordPingAsync(int studentId, RemedialTrackVideoPingRequest request, CancellationToken ct = default)
        {
            var state = request.State?.Trim().ToLowerInvariant();
            if (state is not ("playing" or "paused" or "ended" or "manual-done"))
                return Fail(RemedialTrackPingStatus.BadRequest, "state", "حالة المشاهدة غير صالحة.");

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            // 1) قراءة خفيفة واحدة (Projection) تشمل لقطة بوابة الوصول — D14
            var row = await db.RemedialTrackVideoProgresses.AsNoTracking()
                .Where(v => v.Id == request.VideoProgressId
                            && v.AxisProgress!.EnrollmentId == request.EnrollmentId
                            && v.AxisProgress.Enrollment!.StudentId == studentId)
                .Select(v => new
                {
                    v.Id,
                    v.AxisProgressId,
                    v.Round,
                    v.VideoOrder,
                    v.WatchedSeconds,
                    v.EndedSeen,
                    v.IsCompleted,
                    v.LastPingAtUtc,
                    v.LastPingState,
                    v.DurationSeconds,
                    VideoTitle = v.Video!.Title,
                    Provider = v.Video.Provider,
                    AdminDuration = v.Video.DurationSeconds,
                    AxisId = v.AxisProgress!.AxisId,
                    AxisStatus = v.AxisProgress.Status,
                    AxisRound = v.AxisProgress.Round,
                    MinWatch = v.AxisProgress.Enrollment!.Publication!.Track!.MinWatchPercent,
                    Access = new RemedialTrackAccessSnapshot(
                        v.AxisProgress.Enrollment.StudentId,
                        v.AxisProgress.Enrollment.Status,
                        v.AxisProgress.Enrollment.Publication.Status,
                        v.AxisProgress.Enrollment.Publication.PublishAtUtc,
                        v.AxisProgress.Enrollment.Publication.Mode,
                        v.AxisProgress.Enrollment.Publication.CodeVersion,
                        v.AxisProgress.Enrollment.VerifiedCodeVersion,
                        v.AxisProgress.Enrollment.CodeLockedUntilUtc,
                        v.AxisProgress.Enrollment.Publication.IsDeleted)
                })
                .FirstOrDefaultAsync(ct);

            if (row is null)
                return Fail(RemedialTrackPingStatus.NotFound, "notfound", "الفيديو غير موجود.");

            // 2) بوابة الوصول
            switch (RemedialTrackAccessService.Decide(row.Access, studentId, now))
            {
                case RemedialTrackAccessOutcome.NotFound:
                    return Fail(RemedialTrackPingStatus.NotFound, "notfound", "الفيديو غير موجود.");
                case RemedialTrackAccessOutcome.Cancelled:
                case RemedialTrackAccessOutcome.NotYetPublished:
                    return Fail(RemedialTrackPingStatus.Forbidden, "unavailable", "هذه الخطة غير متاحة حاليًا.");
                case RemedialTrackAccessOutcome.NeedsCode:
                case RemedialTrackAccessOutcome.CodeLocked:
                    return new RemedialTrackPingResult(RemedialTrackPingStatus.NeedsCode,
                        new RemedialTrackVideoPingResponse { Ok = false, NeedsCode = true, Reason = "needs-code", Message = "أدخل الرقم المرجعي للمتابعة." });
            }

            // 3) الحالة: Videos (جولة 1) أو Rewatch (جولة 2) وصف الفيديو من نفس الجولة
            var statusOk = (row.AxisStatus == RemedialTrackAxisStatus.Videos && row.AxisRound == 1 && row.Round == 1)
                           || (row.AxisStatus == RemedialTrackAxisStatus.Rewatch && row.AxisRound == 2 && row.Round == 2);
            if (!statusOk)
                return Fail(RemedialTrackPingStatus.Conflict, "state-conflict", "لا يمكن تسجيل المشاهدة في حالة المحور الحالية.");

            // 4) التسلسل: كل فيديو أسبق في نفس الجولة يجب أن يكون مكتملًا
            var hasPendingBefore = await db.RemedialTrackVideoProgresses.AsNoTracking()
                .AnyAsync(x => x.AxisProgressId == row.AxisProgressId && x.Round == row.Round
                               && x.VideoOrder < row.VideoOrder && !x.IsCompleted && x.Video!.IsActive, ct);
            if (hasPendingBefore)
                return Fail(RemedialTrackPingStatus.Conflict, "locked", "أكمل الفيديو السابق أولًا.");

            // 5) مكتمل سابقًا: ردّ بنفس الحقيقة (idempotent) ليتقدّم العميل
            if (row.IsCompleted)
            {
                var info = await BuildAfterCompletionAsync(db, row.AxisProgressId, row.Round, row.VideoOrder, ct);
                return new RemedialTrackPingResult(RemedialTrackPingStatus.Ok, new RemedialTrackVideoPingResponse
                {
                    Ok = true, Completed = true,
                    WatchedSeconds = (int)Math.Floor(row.WatchedSeconds),
                    RequiredSeconds = (int)Math.Ceiling((row.AdminDuration ?? row.DurationSeconds ?? 0) * row.MinWatch / 100.0),
                    NextVideo = info.Next, AllDone = info.AllDone
                });
            }

            if (state == "manual-done" && row.Provider != RemedialTrackVideoProvider.Other)
                return Fail(RemedialTrackPingStatus.BadRequest, "state", "حالة المشاهدة غير صالحة لهذا الفيديو.");

            var c = ComputePing(row.WatchedSeconds, row.EndedSeen, row.LastPingState, row.LastPingAtUtc, now,
                state, row.Provider, row.AdminDuration, row.DurationSeconds, request.Duration, row.MinWatch);

            var storedState = state == "manual-done" ? "ended" : state;
            var args = new PingWrite(row.Id, row.LastPingAtUtc, c, storedState, now);

            // 6) تحديث ذرّي واحد؛ عند الإتمام: Transaction + حدث + تحويل الحالة
            if (!c.Completed)
            {
                await ApplyPingUpdateAsync(db, args, ct);
                return new RemedialTrackPingResult(RemedialTrackPingStatus.Ok, new RemedialTrackVideoPingResponse
                {
                    Ok = true, Completed = false,
                    Reason = c.Ended ? "insufficient" : null,
                    Message = c.Ended ? "لم تكتمل المشاهدة المطلوبة، أعد المشاهدة من البداية." : null,
                    WatchedSeconds = (int)Math.Floor(c.WatchedSeconds),
                    RequiredSeconds = (int)c.RequiredSeconds
                });
            }

            var completedNow = await CompleteVideoAsync(request.EnrollmentId, row.AxisProgressId, row.AxisId, row.Round, row.AxisStatus, row.AxisRound,
                row.VideoTitle, args, ct);

            var after = await BuildAfterCompletionAsync(db, row.AxisProgressId, row.Round, row.VideoOrder, ct);
            return new RemedialTrackPingResult(RemedialTrackPingStatus.Ok, new RemedialTrackVideoPingResponse
            {
                Ok = true,
                Completed = completedNow,
                WatchedSeconds = (int)Math.Floor(c.WatchedSeconds),
                RequiredSeconds = (int)c.RequiredSeconds,
                NextVideo = completedNow ? after.Next : null,
                AllDone = completedNow && after.AllDone
            });
        }

        private sealed record PingWrite(
            int VideoProgressId, DateTime? ExpectedLastPingAtUtc, RemedialTrackPingComputation C, string State, DateTime Now);

        // يعيد عدد الصفوف المتأثرة. شرط LastPingAtUtc يمنع ازدواج الرصيد عند طلبين متزامنين لنفس الفيديو.
        private static async Task<int> ApplyPingUpdateAsync(ApplicationDbContext db, PingWrite w, CancellationToken ct)
        {
            var watched = w.C.WatchedSeconds;
            var duration = (int)Math.Ceiling(w.C.DurationSeconds);
            var ended = w.C.Ended;
            var completed = w.C.Completed;
            var now = w.Now;
            var state = w.State;
            var expected = w.ExpectedLastPingAtUtc;
            var id = w.VideoProgressId;

            if (db.Database.IsRelational())
            {
                return await db.RemedialTrackVideoProgresses
                    .Where(x => x.Id == id && !x.IsCompleted && x.LastPingAtUtc == expected)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.WatchedSeconds, watched)
                        .SetProperty(x => x.DurationSeconds, (int?)duration)
                        .SetProperty(x => x.EndedSeen, ended)
                        .SetProperty(x => x.LastPingAtUtc, (DateTime?)now)
                        .SetProperty(x => x.LastPingState, state)
                        .SetProperty(x => x.FirstPingAtUtc, x => x.FirstPingAtUtc ?? now)
                        .SetProperty(x => x.IsCompleted, completed)
                        .SetProperty(x => x.CompletedAtUtc, completed ? (DateTime?)now : null), ct);
            }

            // InMemory (الاختبارات) لا يدعم ExecuteUpdate
            var e = await db.RemedialTrackVideoProgresses.FirstOrDefaultAsync(x => x.Id == id && !x.IsCompleted && x.LastPingAtUtc == expected, ct);
            if (e is null) return 0;
            e.WatchedSeconds = watched;
            e.DurationSeconds = duration;
            e.EndedSeen = ended;
            e.LastPingAtUtc = now;
            e.LastPingState = state;
            e.FirstPingAtUtc ??= now;
            e.IsCompleted = completed;
            e.CompletedAtUtc = completed ? now : null;
            await db.SaveChangesAsync(ct);
            db.Entry(e).State = EntityState.Detached;
            return 1;
        }

        // إتمام الفيديو: تحديث + حدث VideoCompleted + (إن كان الأخير) OnAllVideosCompleted — Transaction + RowVersion
        private async Task<bool> CompleteVideoAsync(
            int enrollmentId, int axisProgressId, int axisId, int round,
            RemedialTrackAxisStatus axisStatus, int axisRound, string videoTitle, PingWrite write, CancellationToken ct)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await using var db = await _dbFactory.CreateDbContextAsync(ct);
                    var strategy = db.Database.CreateExecutionStrategy();

                    return await strategy.ExecuteAsync(async () =>
                    {
                        db.ChangeTracker.Clear();
                        await using var tx = db.Database.IsRelational()
                            ? await db.Database.BeginTransactionAsync(ct)
                            : null;

                        var affected = await ApplyPingUpdateAsync(db, write, ct);
                        if (affected == 0) return false;   // طلب متزامن سبقنا — لا تكرار للحدث

                        db.RemedialTrackEvents.Add(new RemedialTrackEvent
                        {
                            EnrollmentId = enrollmentId,
                            AxisId = axisId,
                            Type = RemedialTrackEventType.VideoCompleted,
                            Message = Truncate($"أتمّ الطالب مشاهدة الفيديو «{videoTitle}» (الجولة {round}).", 500),
                            CreatedAtUtc = write.Now
                        });
                        await db.SaveChangesAsync(ct);

                        var remaining = await db.RemedialTrackVideoProgresses.AsNoTracking()
                            .AnyAsync(x => x.AxisProgressId == axisProgressId && x.Round == round
                                           && !x.IsCompleted && x.Video!.IsActive, ct);

                        if (!remaining)
                        {
                            var ap = await db.RemedialTrackAxisProgresses.FirstAsync(a => a.Id == axisProgressId, ct);
                            // الحالة قد تكون تحوّلت من طلب آخر — لا ننتقل مرتين
                            if (ap.Status == axisStatus && ap.Round == axisRound)
                            {
                                var t = RemedialTrackStateMachine.OnAllVideosCompleted(ap.Status, ap.Round);
                                ap.Status = t.NewStatus;
                                ap.Round = t.NewRound;
                                await db.SaveChangesAsync(ct);   // RowVersion: تعارض ← DbUpdateConcurrencyException
                            }
                        }

                        if (tx is not null) await tx.CommitAsync(ct);
                        return true;
                    });
                }
                catch (DbUpdateConcurrencyException) when (attempt < 2)
                {
                    // أعد القراءة وأعد المحاولة مرة واحدة (الفيديو يُعاد تقييمه: إن اكتمل سابقًا يُرجع affected=0)
                }
            }
        }

        // التالي في نفس الجولة + هل انتهت كل فيديوهات الجولة
        private static async Task<(RemedialTrackNextVideoDto? Next, bool AllDone)> BuildAfterCompletionAsync(
            ApplicationDbContext db, int axisProgressId, int round, int videoOrder, CancellationToken ct)
        {
            var next = await db.RemedialTrackVideoProgresses.AsNoTracking()
                .Where(x => x.AxisProgressId == axisProgressId && x.Round == round
                            && x.VideoOrder > videoOrder && x.Video!.IsActive)
                .OrderBy(x => x.VideoOrder)
                .Select(x => new { x.Id, x.Video!.Title, x.Video.Provider, x.Video.ExternalId, x.Video.Url })
                .FirstOrDefaultAsync(ct);

            var anyPending = await db.RemedialTrackVideoProgresses.AsNoTracking()
                .AnyAsync(x => x.AxisProgressId == axisProgressId && x.Round == round
                               && !x.IsCompleted && x.Video!.IsActive, ct);

            return (next is null ? null : new RemedialTrackNextVideoDto
            {
                VideoProgressId = next.Id,
                Title = next.Title,
                Provider = next.Provider.ToString(),
                ExternalId = next.ExternalId,
                Url = next.Url,
                EmbedUrl = RemedialTrackVideoUrlParser.BuildEmbedUrl(next.Provider, next.ExternalId)
            }, !anyPending);
        }

        // ═════════════════ RTK-S5.1: الانتقالات فوق آلة الحالة ═════════════════

        private static string Pct(double? v) => (v ?? 0).ToString("0.#", CultureInfo.InvariantCulture);

        // Transaction واحدة (للقواعد العلائقية فقط) + إعادة محاولة واحدة عند تعارض RowVersion (تُعاد القراءة كاملة)
        private async Task<T> InTransactionAsync<T>(Func<ApplicationDbContext, Task<T>> work, CancellationToken ct)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await using var db = await _dbFactory.CreateDbContextAsync(ct);
                    var strategy = db.Database.CreateExecutionStrategy();

                    return await strategy.ExecuteAsync(async () =>
                    {
                        db.ChangeTracker.Clear();
                        await using var tx = db.Database.IsRelational()
                            ? await db.Database.BeginTransactionAsync(ct)
                            : null;

                        var result = await work(db);
                        if (tx is not null) await tx.CommitAsync(ct);
                        return result;
                    });
                }
                catch (DbUpdateConcurrencyException) when (attempt < 2)
                {
                    // تعارض مع طلب متزامن: أعد القراءة والتنفيذ (الدوال Idempotent فتتعرّف على ما تمّ)
                }
            }
        }

        public Task<RemedialTrackTransitionResult> OnAllVideosCompletedAsync(int axisProgressId, CancellationToken ct = default) =>
            InTransactionAsync(async db =>
            {
                var ap = await db.RemedialTrackAxisProgresses.FirstOrDefaultAsync(a => a.Id == axisProgressId, ct);
                if (ap is null) return new RemedialTrackTransitionResult(false, null, 0, "المحور غير موجود.");
                if (ap.Status == RemedialTrackAxisStatus.Locked)
                    return new RemedialTrackTransitionResult(false, ap.Status, ap.Round, "المحور مغلق.");
                if (ap.Status is not (RemedialTrackAxisStatus.Videos or RemedialTrackAxisStatus.Rewatch))
                    return new RemedialTrackTransitionResult(false, ap.Status, ap.Round);   // مُنفَّذ سابقًا

                var total = await db.RemedialTrackVideoProgresses.AsNoTracking()
                    .CountAsync(x => x.AxisProgressId == ap.Id && x.Round == ap.Round && x.Video!.IsActive, ct);
                var pending = await db.RemedialTrackVideoProgresses.AsNoTracking()
                    .CountAsync(x => x.AxisProgressId == ap.Id && x.Round == ap.Round && x.Video!.IsActive && !x.IsCompleted, ct);
                if (total == 0 || pending > 0)
                    return new RemedialTrackTransitionResult(false, ap.Status, ap.Round, "لم تكتمل مشاهدة كل فيديوهات الجولة.");

                var t = RemedialTrackStateMachine.OnAllVideosCompleted(ap.Status, ap.Round);
                ap.Status = t.NewStatus;
                ap.Round = t.NewRound;
                await db.SaveChangesAsync(ct);
                return new RemedialTrackTransitionResult(true, ap.Status, ap.Round);
            }, ct);

        public Task<RemedialTrackTransitionResult> OnExamSubmittedAsync(int attemptId, CancellationToken ct = default) =>
            InTransactionAsync(async db =>
            {
                var now = _time.GetUtcNow().UtcDateTime;

                var att = await db.RemedialTrackExamAttempts.AsNoTracking()
                    .Where(a => a.Id == attemptId)
                    .Select(a => new { a.AxisProgressId, a.ExamNumber, a.Status, a.ScorePercent, a.IsPassed })
                    .FirstOrDefaultAsync(ct);
                if (att is null) return new RemedialTrackTransitionResult(false, null, 0, "المحاولة غير موجودة.");

                // RTK-S13/D29 (حارس إلزامي): محاولة «الملحق» لا تنقل حالة المحور ولا تُحسب 102 أبدًا
                if (att.ExamNumber == RemedialTrackExamNumber.Addendum)
                    return new RemedialTrackTransitionResult(false, null, 0, "محاولة ملحق: لا تنتقل بها حالة المحور.");

                if (att.Status == RemedialTrackAttemptStatus.InProgress)
                    return new RemedialTrackTransitionResult(false, null, 0, "المحاولة لم تُسلَّم بعد.");

                var ap = await db.RemedialTrackAxisProgresses.FirstAsync(a => a.Id == att.AxisProgressId, ct);

                // Idempotent: التسليم المزدوج/إعادة الاستدعاء لا أثر له بعد أن تحوّلت الحالة
                // switch صريح: أي رقم غير 101/102 لا يُفترض 102 (قيمة غير معروفة ← لا انتقال)
                RemedialTrackAxisStatus expected;
                switch (att.ExamNumber)
                {
                    case RemedialTrackExamNumber.Exam101: expected = RemedialTrackAxisStatus.AwaitingExam101; break;
                    case RemedialTrackExamNumber.Exam102: expected = RemedialTrackAxisStatus.AwaitingExam102; break;
                    default:
                        return new RemedialTrackTransitionResult(false, ap.Status, ap.Round, "رقم الاختبار غير مدعوم لانتقال المحور.");
                }
                if (ap.Status != expected)
                    return new RemedialTrackTransitionResult(false, ap.Status, ap.Round);

                var info = await db.RemedialTrackAxisProgresses.AsNoTracking()
                    .Where(a => a.Id == ap.Id)
                    .Select(a => new
                    {
                        a.EnrollmentId,
                        a.AxisId,
                        Title = a.Axis!.TitleOverride ?? a.Axis.Section!.Title,
                        Pass = a.Enrollment!.Publication!.Track!.PassPercent
                    })
                    .FirstAsync(ct);

                var t = RemedialTrackStateMachine.OnExamSubmitted(ap.Status, att.ExamNumber, att.IsPassed);
                var examName = att.ExamNumber.DisplayName();
                var score = Pct(att.ScorePercent);

                if (att.ExamNumber == RemedialTrackExamNumber.Exam101) ap.Exam101Percent = att.ScorePercent;
                else if (att.ExamNumber == RemedialTrackExamNumber.Exam102) ap.Exam102Percent = att.ScorePercent;
                ap.Status = t.NewStatus;
                ap.Round = t.NewRound;

                if (att.IsPassed)
                {
                    ap.PassedAtUtc = now;
                    AddEvent(db, info.EnrollmentId, info.AxisId, RemedialTrackEventType.ExamPassed,
                        $"اجتاز الطالب {examName} للمحور «{info.Title}» بنسبة {score}%.", now);
                    if (t.UnlockNextAxis)
                        await OpenNextAxisAsync(db, info.EnrollmentId, ap.Order, now, ct);   // v2: بلا جدولة (D19)
                }
                else
                {
                    // صياغة محايدة: هذه الأحداث قد تظهر في خط الطالب الزمني (S6)
                    AddEvent(db, info.EnrollmentId, info.AxisId, RemedialTrackEventType.ExamFailed,
                        $"نتيجة {examName} للمحور «{info.Title}»: {score}% (نسبة الاجتياز {info.Pass}%).", now);

                    if (t.StartRewatchRound)
                    {
                        AddEvent(db, info.EnrollmentId, info.AxisId, RemedialTrackEventType.AxisRewatchOpened,
                            $"أُعيدت فيديوهات المحور «{info.Title}» للمشاهدة (الجولة 2).", now);
                        await AddRewatchRowsAsync(db, ap.Id, info.AxisId, ct);
                    }
                    if (t.RecordNotPassed)
                    {
                        ap.FailedAtUtc = now;
                        AddEvent(db, info.EnrollmentId, info.AxisId, RemedialTrackEventType.AxisNotPassed,
                            $"لم يجتز الطالب الخطة العلاجية للمحور «{info.Title}» (الأول: {Pct(ap.Exam101Percent)}%، الثاني: {Pct(ap.Exam102Percent)}%).", now);
                    }
                }

                await db.SaveChangesAsync(ct);

                // RTK-S11.2: تقرير ولي الأمر عند عدم الاجتياز في الاختبارين (نفس المعاملة؛ فشل اللقطة لا يُفشل التسليم)
                if (t.RecordNotPassed)
                    await RemedialTrackParentReportWriter.AddAsync(
                        db, info.EnrollmentId, ap.Id, ap.Order, RemedialTrackParentReportKind.AxisNotPassed, now, _logger, ct);

                if (att.IsPassed || t.RecordNotPassed)
                    await FinalizeEnrollmentAsync(db, info.EnrollmentId, now, ct);

                return new RemedialTrackTransitionResult(true, ap.Status, ap.Round);
            }, ct);

        public Task<RemedialTrackTransitionResult> AdminOpenNextAsync(
            int axisProgressId, string? reason, RemedialTrackActor actor, CancellationToken ct = default)
        {
            var cleanReason = reason?.Trim();
            if (string.IsNullOrEmpty(cleanReason))
                return Task.FromResult(new RemedialTrackTransitionResult(false, null, 0, "سبب فتح المحور التالي مطلوب."));
            if (cleanReason.Length > 300)
                return Task.FromResult(new RemedialTrackTransitionResult(false, null, 0, "سبب الفتح يجب ألا يتجاوز 300 حرف."));

            return InTransactionAsync(async db =>
            {
                var now = _time.GetUtcNow().UtcDateTime;

                var ap = await db.RemedialTrackAxisProgresses.FirstOrDefaultAsync(a => a.Id == axisProgressId, ct);
                if (ap is null) return new RemedialTrackTransitionResult(false, null, 0, "المحور غير موجود.");
                if (ap.Status == RemedialTrackAxisStatus.FailedOpenedByAdmin)
                    return new RemedialTrackTransitionResult(false, ap.Status, ap.Round);   // Idempotent
                if (ap.Status != RemedialTrackAxisStatus.FailedBlocked)
                    return new RemedialTrackTransitionResult(false, ap.Status, ap.Round,
                        "فتح المحور التالي مسموح فقط لمحور لم يجتزه الطالب في الاختبارين.");

                var info = await db.RemedialTrackAxisProgresses.AsNoTracking()
                    .Where(a => a.Id == ap.Id)
                    .Select(a => new { a.EnrollmentId, a.AxisId, Title = a.Axis!.TitleOverride ?? a.Axis.Section!.Title })
                    .FirstAsync(ct);

                var t = RemedialTrackStateMachine.OnAdminOpenNext(ap.Status);
                ap.Status = t.NewStatus;
                ap.Round = t.NewRound;
                ap.AdminOpenedByUserId = Truncate(actor.UserId, 450);
                ap.AdminOpenedByName = Truncate(actor.Name, 200);
                ap.AdminOpenReason = cleanReason;
                ap.AdminOpenedAtUtc = now;

                db.RemedialTrackEvents.Add(new RemedialTrackEvent
                {
                    EnrollmentId = info.EnrollmentId,
                    AxisId = info.AxisId,
                    Type = RemedialTrackEventType.AdminOpenedNext,
                    Message = Truncate($"فتحت الإدارة المحور التالي بعد المحور «{info.Title}».", 500),
                    ActorUserId = Truncate(actor.UserId, 450),
                    ActorName = Truncate(actor.Name, 200),
                    CreatedAtUtc = now
                });

                // نقل الإدارة قرار صريح: يفتح التالي فورًا
                await OpenNextAxisAsync(db, info.EnrollmentId, ap.Order, now, ct);
                await db.SaveChangesAsync(ct);

                // RTK-S11.4: إشعار نصي لولي الأمر بقرار الإدارة (لا تقرير تابع في هذا الإصدار)
                await RemedialTrackParentReportWriter.AddAdminOpenedNoticeAsync(db, info.EnrollmentId, ap.Id, info.Title, now, ct);

                await FinalizeEnrollmentAsync(db, info.EnrollmentId, now, ct);

                return new RemedialTrackTransitionResult(true, ap.Status, ap.Round);
            }, ct);
        }

        // يفتح المحور المغلق الذي أنهى الطالب سابقَه (اجتاز/نقلته الإدارة) — يعالج ما بقي معلّقًا من جدولة الأيام (v2)
        private async Task OpenPendingAxesAsync(int studentId, int enrollmentId, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var active = await db.RemedialTrackEnrollments.AsNoTracking()
                .AnyAsync(e => e.Id == enrollmentId && e.StudentId == studentId
                               && e.Status != RemedialTrackEnrollmentStatus.Cancelled
                               && e.Publication!.Status == RemedialTrackPublicationStatus.Active
                               && !e.Publication.IsDeleted
                               && e.Publication.PublishAtUtc <= now, ct);
            if (!active) return;

            var rows = await db.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(a => a.EnrollmentId == enrollmentId)
                .OrderBy(a => a.Order)
                .Select(a => new { a.Order, a.Status })
                .ToListAsync(ct);

            for (var i = 1; i < rows.Count; i++)
            {
                if (rows[i].Status != RemedialTrackAxisStatus.Locked) continue;

                var prev = rows[i - 1].Status;
                if (prev is not (RemedialTrackAxisStatus.Passed or RemedialTrackAxisStatus.FailedOpenedByAdmin)) return;

                var prevOrder = rows[i - 1].Order;
                await InTransactionAsync(async tdb =>
                {
                    if (await OpenNextAxisAsync(tdb, enrollmentId, prevOrder, now, ct))
                        await tdb.SaveChangesAsync(ct);
                    return true;
                }, ct);
                return;
            }
        }

        // يفتح المحور التالي (Locked → Videos/جولة 1) ويحدّث CurrentAxisId؛ لا يحفظ (المستدعي يحفظ). يعيد true إن فُتح محور.
        private static async Task<bool> OpenNextAxisAsync(
            ApplicationDbContext db, int enrollmentId, int currentOrder, DateTime now, CancellationToken ct)
        {
            var next = await db.RemedialTrackAxisProgresses
                .Where(a => a.EnrollmentId == enrollmentId && a.Order > currentOrder)
                .OrderBy(a => a.Order)
                .FirstOrDefaultAsync(ct);
            if (next is null) return false;
            if (next.Status != RemedialTrackAxisStatus.Locked) return false;   // مفتوح سابقًا

            next.Status = RemedialTrackAxisStatus.Videos;
            next.Round = 1;
            next.OpenedAtUtc = now;

            var title = await db.RemedialTrackAxes.AsNoTracking()
                .Where(x => x.Id == next.AxisId)
                .Select(x => x.TitleOverride ?? x.Section!.Title)
                .FirstOrDefaultAsync(ct);

            var enrollment = await db.RemedialTrackEnrollments.FirstAsync(e => e.Id == enrollmentId, ct);
            enrollment.CurrentAxisId = next.AxisId;

            AddEvent(db, enrollmentId, next.AxisId, RemedialTrackEventType.AxisOpened,
                $"فُتح المحور «{title}».", now);
            return true;
        }

        // بعد حفظ حالات المحاور: حالة التسجيل (اكتمال/اكتمال مع تعثّر) + CompletedAtUtc + حدث TrackCompleted
        private async Task FinalizeEnrollmentAsync(ApplicationDbContext db, int enrollmentId, DateTime now, CancellationToken ct)
        {
            var enrollment = await db.RemedialTrackEnrollments.FirstAsync(e => e.Id == enrollmentId, ct);
            if (enrollment.Status == RemedialTrackEnrollmentStatus.Cancelled) return;

            var statuses = await db.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(a => a.EnrollmentId == enrollmentId)
                .OrderBy(a => a.Order)
                .Select(a => a.Status)
                .ToListAsync(ct);

            var resolved = RemedialTrackStateMachine.ResolveEnrollmentStatus(statuses);
            var completedNow = false;
            if (resolved is RemedialTrackEnrollmentStatus.Completed or RemedialTrackEnrollmentStatus.CompletedWithFailures)
            {
                if (enrollment.Status == resolved) return;
                completedNow = true;
                enrollment.Status = resolved;
                enrollment.CompletedAtUtc ??= now;
                AddEvent(db, enrollmentId, null, RemedialTrackEventType.TrackCompleted,
                    resolved == RemedialTrackEnrollmentStatus.Completed
                        ? "أنهى الطالب جميع محاور الخطة العلاجية."
                        : "أنهى الطالب الخطة العلاجية، وبعض المحاور تحتاج متابعة مع الإدارة.", now);
            }
            else if (resolved == RemedialTrackEnrollmentStatus.InProgress && enrollment.Status == RemedialTrackEnrollmentStatus.NotStarted)
            {
                enrollment.Status = RemedialTrackEnrollmentStatus.InProgress;
            }
            await db.SaveChangesAsync(ct);

            // RTK-S11.2: التقرير الختامي عند اكتمال الخطة (مرة واحدة؛ الفهرس الفريد حارس التزامن)
            if (completedNow)
                await RemedialTrackParentReportWriter.AddAsync(
                    db, enrollmentId, null, null, RemedialTrackParentReportKind.Final, now, _logger, ct);
        }

        // الجولة 2: صفوف تقدّم لكل الفيديوهات الفعّالة دفعة واحدة (AddRange؛ يحفظها SaveChanges المستدعي). فيديوهات الجولة 1 تبقى للسجل.
        private static async Task AddRewatchRowsAsync(ApplicationDbContext db, int axisProgressId, int axisId, CancellationToken ct)
        {
            var videos = await db.RemedialTrackVideos.AsNoTracking()
                .Where(v => v.AxisId == axisId && v.IsActive)
                .OrderBy(v => v.Order)
                .Select(v => new { v.Id, v.Order, v.DurationSeconds })
                .ToListAsync(ct);

            var existing = new HashSet<int>(await db.RemedialTrackVideoProgresses.AsNoTracking()
                .Where(v => v.AxisProgressId == axisProgressId && v.Round == 2)
                .Select(v => v.VideoId)
                .ToListAsync(ct));

            db.RemedialTrackVideoProgresses.AddRange(videos
                .Where(v => !existing.Contains(v.Id))
                .Select(v => new RemedialTrackVideoProgress
                {
                    AxisProgressId = axisProgressId,
                    VideoId = v.Id,
                    VideoOrder = v.Order,
                    Round = 2,
                    DurationSeconds = v.DurationSeconds
                }));
        }

        private static void AddEvent(ApplicationDbContext db, int enrollmentId, int? axisId, RemedialTrackEventType type, string message, DateTime now) =>
            db.RemedialTrackEvents.Add(new RemedialTrackEvent
            {
                EnrollmentId = enrollmentId,
                AxisId = axisId,
                Type = type,
                Message = Truncate(message, 500),
                CreatedAtUtc = now
            });

        private static RemedialTrackPingResult Fail(RemedialTrackPingStatus status, string reason, string message) =>
            new(status, new RemedialTrackVideoPingResponse { Ok = false, Reason = reason, Message = message });

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
    }
}
