using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>قواعد نقية لحالة الملحق عند الطالب (تُستعمل في بطاقة الخطة وصفحة الملحق).</summary>
    public static class RemedialTrackAddendumRules
    {
        public static StudentAddendumState StateOf(double watchedSeconds, bool videoCompleted, int attemptsCount, bool completed)
        {
            if (completed) return StudentAddendumState.Completed;
            if (!videoCompleted) return watchedSeconds > 0 ? StudentAddendumState.Watching : StudentAddendumState.NotStarted;
            return attemptsCount > 0 ? StudentAddendumState.RetakeExam : StudentAddendumState.Watched;
        }
    }

    /// <summary>RTK-S13: مسار الطالب في الملحق (مشاهدة بزمن الخادم + اختبار مستقل). انظر <see cref="IRemedialTrackAddendumStudentService"/>.</summary>
    public sealed class RemedialTrackAddendumStudentService : IRemedialTrackAddendumStudentService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly ITimeZoneService _tz;
        private readonly ILogger<RemedialTrackAddendumStudentService> _logger;

        public RemedialTrackAddendumStudentService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            ITimeZoneService tz,
            ILogger<RemedialTrackAddendumStudentService> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _tz = tz;
            _logger = logger;
        }

        // ═════════════════ لقطة الملكية + البوابة ═════════════════

        private sealed record Head(
            int ProgressId, int EnrollmentId, int AddendumId, int AxisId,
            int? AxisProgressId, RemedialTrackAxisStatus? AxisStatus, bool AddendumActive,
            string Title, string Reason, RemedialTrackVideoProvider Provider, string? ExternalId, string Url, int? AdminDuration,
            int? ExamModelId, int? ExamMinutes,
            double WatchedSeconds, int? StoredDuration, bool VideoCompleted, bool ExamPassed, double? BestScore, int AttemptsCount, DateTime? CompletedAtUtc,
            string TrackTitle, string AxisTitle, int MinWatch,
            RemedialTrackAccessSnapshot Access);

        private static Task<Head?> LoadHeadAsync(ApplicationDbContext db, int studentId, int enrollmentId, int addendumId, CancellationToken ct) =>
            db.RemedialTrackAddendumProgresses.AsNoTracking()
                .Where(p => p.AddendumId == addendumId && p.EnrollmentId == enrollmentId && p.Enrollment!.StudentId == studentId)
                .Select(p => new Head(
                    p.Id,
                    p.EnrollmentId,
                    p.AddendumId,
                    p.Addendum!.AxisId,
                    p.Enrollment!.AxisProgresses.Where(a => a.AxisId == p.Addendum.AxisId).Select(a => (int?)a.Id).FirstOrDefault(),
                    p.Enrollment.AxisProgresses.Where(a => a.AxisId == p.Addendum.AxisId).Select(a => (RemedialTrackAxisStatus?)a.Status).FirstOrDefault(),
                    p.Addendum.IsActive,
                    p.Addendum.Title,
                    p.Addendum.Reason,
                    p.Addendum.Provider,
                    p.Addendum.ExternalId,
                    p.Addendum.Url,
                    p.Addendum.DurationSeconds,
                    p.Addendum.ExamModelId,
                    p.Addendum.ExamDurationMinutes,
                    p.WatchedSeconds,
                    p.DurationSeconds,
                    p.VideoCompleted,
                    p.ExamPassed,
                    p.BestScorePercent,
                    p.AttemptsCount,
                    p.CompletedAtUtc,
                    p.Enrollment.Publication!.Track!.Title,
                    p.Addendum.Axis!.TitleOverride ?? p.Addendum.Axis.Section!.Title,
                    p.Enrollment.Publication.Track.MinWatchPercent,
                    new RemedialTrackAccessSnapshot(
                        p.Enrollment.StudentId,
                        p.Enrollment.Status,
                        p.Enrollment.Publication.Status,
                        p.Enrollment.Publication.PublishAtUtc,
                        p.Enrollment.Publication.Mode,
                        p.Enrollment.Publication.CodeVersion,
                        p.Enrollment.VerifiedCodeVersion,
                        p.Enrollment.CodeLockedUntilUtc,
                        p.Enrollment.Publication.IsDeleted)))
                .FirstOrDefaultAsync(ct);

        // الملحق مرئي للطالب: ملحق فعّال + محور الملحق مفتوح (ليس Locked — D31)
        private static bool IsVisible(Head h) =>
            h.AddendumActive && h.AxisStatus is not null && h.AxisStatus != RemedialTrackAxisStatus.Locked;

        // ═════════════════ صفحة الملحق ═════════════════

        public async Task<StudentAddendumVm?> GetAsync(int studentId, int enrollmentId, int addendumId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var h = await LoadHeadAsync(db, studentId, enrollmentId, addendumId, ct);
            if (h is null || !IsVisible(h)) return null;
            if (RemedialTrackAccessService.Decide(h.Access, studentId, now) != RemedialTrackAccessOutcome.Allowed) return null;

            var who = await db.Students.AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => new { s.FullName, s.NationalID })
                .FirstOrDefaultAsync(ct);

            // استعلام واحد صغير لمحاولات هذا الملحق (المحور نفسه للملكية فقط)
            var attemptRows = await db.RemedialTrackExamAttempts.AsNoTracking()
                .Where(a => a.AddendumId == addendumId && a.AxisProgressId == h.AxisProgressId)
                .OrderByDescending(a => a.StartedAtUtc)
                .Select(a => new { a.Id, a.Status, a.ScorePercent, a.IsPassed, a.SubmittedAtUtc, a.ExpiresAtUtc })
                .Take(50)
                .ToListAsync(ct);

            var duration = h.AdminDuration ?? h.StoredDuration;
            var hasExam = h.ExamModelId.HasValue;
            var videoOpen = !h.VideoCompleted;   // لا يغادر الخادمَ رابط/معرّف الفيديو بعد اكتماله (كمحور منتهٍ)

            return new StudentAddendumVm
            {
                EnrollmentId = h.EnrollmentId,
                AddendumId = h.AddendumId,
                AddendumProgressId = h.ProgressId,
                TrackTitle = h.TrackTitle,
                AxisTitle = h.AxisTitle,
                Title = h.Title,
                Reason = h.Reason,
                Provider = h.Provider,
                ExternalId = videoOpen ? h.ExternalId : null,
                Url = videoOpen ? h.Url : string.Empty,
                EmbedUrl = videoOpen ? RemedialTrackVideoUrlParser.BuildEmbedUrl(h.Provider, h.ExternalId) : null,
                DurationSeconds = duration,
                WatchedSeconds = (int)Math.Floor(h.WatchedSeconds),
                RequiredSeconds = duration.HasValue ? (int)Math.Ceiling(duration.Value * h.MinWatch / 100.0) : 0,
                VideoCompleted = h.VideoCompleted,
                HasExam = hasExam,
                ExamDurationMinutes = h.ExamMinutes ?? 0,
                ExamPassed = h.ExamPassed,
                BestScorePercent = h.BestScore,
                AttemptsCount = h.AttemptsCount,
                InProgressAttemptId = attemptRows.Where(a => a.Status == RemedialTrackAttemptStatus.InProgress).Select(a => (int?)a.Id).FirstOrDefault(),
                Attempts = attemptRows
                    .Where(a => a.Status != RemedialTrackAttemptStatus.InProgress)
                    .Select(a => new StudentAddendumAttemptVm
                    {
                        AttemptId = a.Id,
                        ScorePercent = a.ScorePercent,
                        IsPassed = a.IsPassed,
                        SubmittedAtLocal = _tz.ConvertToSaudi(a.SubmittedAtUtc ?? a.ExpiresAtUtc)
                    })
                    .ToList(),
                IsCompleted = h.CompletedAtUtc.HasValue,
                State = RemedialTrackAddendumRules.StateOf(h.WatchedSeconds, h.VideoCompleted, h.AttemptsCount, h.CompletedAtUtc.HasValue),
                WatermarkText = who?.FullName ?? string.Empty,
                WatermarkIdText = (who?.NationalID ?? string.Empty).Trim()
            };
        }

        // ═════════════════ النبضة (D32) ═════════════════

        public async Task<RemedialTrackPingResult> RecordPingAsync(int studentId, RemedialTrackAddendumPingRequest request, CancellationToken ct = default)
        {
            var state = request.State?.Trim().ToLowerInvariant();
            if (state is not ("playing" or "paused" or "ended" or "manual-done"))
                return Fail(RemedialTrackPingStatus.BadRequest, "state", "حالة المشاهدة غير صالحة.");

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var row = await db.RemedialTrackAddendumProgresses.AsNoTracking()
                .Where(p => p.Id == request.AddendumProgressId && p.EnrollmentId == request.EnrollmentId
                            && p.Enrollment!.StudentId == studentId)
                .Select(p => new
                {
                    p.Id,
                    p.WatchedSeconds,
                    p.EndedSeen,
                    p.VideoCompleted,
                    p.LastPingAtUtc,
                    p.LastPingState,
                    p.DurationSeconds,
                    Provider = p.Addendum!.Provider,
                    AdminDuration = p.Addendum.DurationSeconds,
                    Active = p.Addendum.IsActive,
                    HasExam = p.Addendum.ExamModelId != null,
                    AxisStatus = p.Enrollment!.AxisProgresses.Where(a => a.AxisId == p.Addendum.AxisId).Select(a => (RemedialTrackAxisStatus?)a.Status).FirstOrDefault(),
                    MinWatch = p.Enrollment.Publication!.Track!.MinWatchPercent,
                    Access = new RemedialTrackAccessSnapshot(
                        p.Enrollment.StudentId,
                        p.Enrollment.Status,
                        p.Enrollment.Publication.Status,
                        p.Enrollment.Publication.PublishAtUtc,
                        p.Enrollment.Publication.Mode,
                        p.Enrollment.Publication.CodeVersion,
                        p.Enrollment.VerifiedCodeVersion,
                        p.Enrollment.CodeLockedUntilUtc,
                        p.Enrollment.Publication.IsDeleted)
                })
                .FirstOrDefaultAsync(ct);

            if (row is null)
                return Fail(RemedialTrackPingStatus.NotFound, "notfound", "الفيديو غير موجود.");

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

            // ملحق غير نشط أو محوره Locked ← كأنه غير موجود
            if (!row.Active || row.AxisStatus is null || row.AxisStatus == RemedialTrackAxisStatus.Locked)
                return Fail(RemedialTrackPingStatus.NotFound, "notfound", "الفيديو غير موجود.");

            // مكتمل سابقًا: ردّ بنفس الحقيقة (idempotent)
            if (row.VideoCompleted)
                return Ok(new RemedialTrackVideoPingResponse
                {
                    Ok = true, Completed = true, AllDone = true,
                    WatchedSeconds = (int)Math.Floor(row.WatchedSeconds),
                    RequiredSeconds = (int)Math.Ceiling((row.AdminDuration ?? row.DurationSeconds ?? 0) * row.MinWatch / 100.0)
                });

            if (state == "manual-done" && row.Provider != RemedialTrackVideoProvider.Other)
                return Fail(RemedialTrackPingStatus.BadRequest, "state", "حالة المشاهدة غير صالحة لهذا الفيديو.");

            var c = RemedialTrackProgressService.ComputePing(row.WatchedSeconds, row.EndedSeen, row.LastPingState, row.LastPingAtUtc, now,
                state, row.Provider, row.AdminDuration, row.DurationSeconds, request.Duration, row.MinWatch);

            var storedState = state == "manual-done" ? "ended" : state;
            var affected = await ApplyPingAsync(db, row.Id, row.LastPingAtUtc, c, storedState, now, row.HasExam, ct);

            var completed = c.Completed;
            if (completed && affected == 0)
            {
                // طلب متزامن سبقنا: المرجع هو القاعدة
                completed = await db.RemedialTrackAddendumProgresses.AsNoTracking().AnyAsync(x => x.Id == row.Id && x.VideoCompleted, ct);
            }

            return Ok(new RemedialTrackVideoPingResponse
            {
                Ok = true,
                Completed = completed,
                AllDone = completed,
                Reason = !completed && c.Ended ? "insufficient" : null,
                Message = !completed && c.Ended ? "لم تكتمل المشاهدة المطلوبة، أعد المشاهدة من البداية." : null,
                WatchedSeconds = (int)Math.Floor(c.WatchedSeconds),
                RequiredSeconds = (int)c.RequiredSeconds
            });
        }

        // يعيد عدد الصفوف المتأثرة. شرط LastPingAtUtc يمنع ازدواج الرصيد عند طلبين متزامنين. تحديث واحد بلا RowVersion
        // فلا يتعارض مع تسليم اختبار جارٍ على نفس الصف. عند الإتمام وبلا اختبار يكتمل الملحق كله في نفس الجملة.
        private static async Task<int> ApplyPingAsync(
            ApplicationDbContext db, int id, DateTime? expected, RemedialTrackPingComputation c, string state, DateTime now, bool hasExam, CancellationToken ct)
        {
            var watched = c.WatchedSeconds;
            var duration = (int)Math.Ceiling(c.DurationSeconds);
            var ended = c.Ended;
            var completed = c.Completed;
            var addendumDone = completed && !hasExam;

            if (db.Database.IsRelational())
            {
                return await db.RemedialTrackAddendumProgresses
                    .Where(x => x.Id == id && !x.VideoCompleted && x.LastPingAtUtc == expected)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.WatchedSeconds, watched)
                        .SetProperty(x => x.DurationSeconds, (int?)duration)
                        .SetProperty(x => x.EndedSeen, ended)
                        .SetProperty(x => x.LastPingAtUtc, (DateTime?)now)
                        .SetProperty(x => x.LastPingState, state)
                        .SetProperty(x => x.VideoCompleted, completed)
                        .SetProperty(x => x.VideoCompletedAtUtc, completed ? (DateTime?)now : null)
                        .SetProperty(x => x.CompletedAtUtc, addendumDone ? (DateTime?)now : null), ct);
            }

            // InMemory (الاختبارات) لا يدعم ExecuteUpdate
            var e = await db.RemedialTrackAddendumProgresses.FirstOrDefaultAsync(x => x.Id == id && !x.VideoCompleted && x.LastPingAtUtc == expected, ct);
            if (e is null) return 0;
            e.WatchedSeconds = watched;
            e.DurationSeconds = duration;
            e.EndedSeen = ended;
            e.LastPingAtUtc = now;
            e.LastPingState = state;
            e.VideoCompleted = completed;
            e.VideoCompletedAtUtc = completed ? now : null;
            e.CompletedAtUtc = addendumDone ? now : null;
            await db.SaveChangesAsync(ct);
            db.Entry(e).State = EntityState.Detached;
            return 1;
        }

        // ═════════════════ اختبار الملحق ═════════════════

        public async Task<RemedialTrackExamStartResult> StartExamAsync(int studentId, int enrollmentId, int addendumId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var h = await LoadHeadAsync(db, studentId, enrollmentId, addendumId, ct);
            if (h is null || !IsVisible(h) || h.AxisProgressId is null)
                return new(RemedialTrackExamStartStatus.NotFound);

            switch (RemedialTrackAccessService.Decide(h.Access, studentId, now))
            {
                case RemedialTrackAccessOutcome.NotFound:
                    return new(RemedialTrackExamStartStatus.NotFound);
                case RemedialTrackAccessOutcome.Cancelled:
                case RemedialTrackAccessOutcome.NotYetPublished:
                    return new(RemedialTrackExamStartStatus.Forbidden);
                case RemedialTrackAccessOutcome.NeedsCode:
                case RemedialTrackAccessOutcome.CodeLocked:
                    return new(RemedialTrackExamStartStatus.NeedsCode);
            }

            if (h.ExamModelId is null)
                return new(RemedialTrackExamStartStatus.Conflict, 0, "هذا الملحق بلا اختبار.");
            if (!h.VideoCompleted)
                return new(RemedialTrackExamStartStatus.Conflict, 0, "اختبار الملحق متاح بعد إكمال مشاهدة الفيديو.");
            if (h.ExamPassed)
                return new(RemedialTrackExamStartStatus.Conflict, 0, "اجتزت اختبار هذا الملحق بالفعل.");

            var axisProgressId = h.AxisProgressId.Value;

            var existingId = await FindOpenAttemptIdAsync(db, addendumId, axisProgressId, ct);
            if (existingId.HasValue)
                return new(RemedialTrackExamStartStatus.Existing, existingId.Value);

            var modelId = h.ExamModelId.Value;
            var questionIds = (await db.ProfessionalModelQuestions.AsNoTracking()
                    .Where(q => q.ModelId == modelId && q.QuestionId != null)
                    .OrderBy(q => q.OrderNumber)
                    .Select(q => q.QuestionId)
                    .ToListAsync(ct))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (questionIds.Count == 0)
            {
                _logger.LogWarning("RTK addendum exam model {ModelId} has no questions (addendum {AddendumId})", modelId, addendumId);
                return new(RemedialTrackExamStartStatus.NoQuestions, 0, "اختبار هذا الملحق غير جاهز بعد. تواصل مع الإدارة.");
            }

            var attempt = new RemedialTrackExamAttempt
            {
                AxisProgressId = axisProgressId,
                ExamNumber = RemedialTrackExamNumber.Addendum,
                AddendumId = addendumId,
                ModelId = modelId,
                Status = RemedialTrackAttemptStatus.InProgress,
                StartedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(Math.Clamp(h.ExamMinutes ?? 30, 1, 600)),
                TotalQuestions = questionIds.Count,
                Questions = questionIds
                    .Select((id, i) => new RemedialTrackExamAttemptQuestion { QuestionId = id, Order = i + 1 })
                    .ToList()
            };
            db.RemedialTrackExamAttempts.Add(attempt);

            try
            {
                await db.SaveChangesAsync(ct);   // الفهرس المُصفّى UX_..._Addendum_OpenAttempt يحمي من محاولتين جاريتين
            }
            catch (DbUpdateException ex)
            {
                _logger.LogInformation(ex, "RTK addendum attempt created concurrently (addendum {AddendumId})", addendumId);
                db.ChangeTracker.Clear();
                var raced = await FindOpenAttemptIdAsync(db, addendumId, axisProgressId, ct);
                if (raced.HasValue) return new(RemedialTrackExamStartStatus.Existing, raced.Value);
                throw;
            }

            return new(RemedialTrackExamStartStatus.Created, attempt.Id);
        }

        private static async Task<int?> FindOpenAttemptIdAsync(ApplicationDbContext db, int addendumId, int axisProgressId, CancellationToken ct)
        {
            var id = await db.RemedialTrackExamAttempts.AsNoTracking()
                .Where(a => a.AddendumId == addendumId && a.AxisProgressId == axisProgressId && a.Status == RemedialTrackAttemptStatus.InProgress)
                .Select(a => a.Id)
                .FirstOrDefaultAsync(ct);
            return id == 0 ? null : id;
        }

        // ═════════════════ بعد التسليم ═════════════════

        public async Task OnAttemptSubmittedAsync(int attemptId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var att = await db.RemedialTrackExamAttempts.AsNoTracking()
                .Where(a => a.Id == attemptId && a.AddendumId != null)
                .Select(a => new { a.AddendumId, a.AxisProgressId, a.Status, EnrollmentId = a.AxisProgress!.EnrollmentId })
                .FirstOrDefaultAsync(ct);
            if (att is null || att.Status == RemedialTrackAttemptStatus.InProgress) return;

            // إعادة حساب من المحاولات المُسلَّمة (لا ++): استدعاء مكرر أو تسليم مزدوج لا يضخّم العدّاد
            var addendumId = att.AddendumId!.Value;
            var agg = await db.RemedialTrackExamAttempts.AsNoTracking()
                .Where(a => a.AddendumId == addendumId && a.AxisProgressId == att.AxisProgressId && a.Status != RemedialTrackAttemptStatus.InProgress)
                .GroupBy(a => a.AddendumId)
                .Select(g => new { Count = g.Count(), Best = g.Max(x => x.ScorePercent), Passed = g.Count(x => x.IsPassed) })
                .FirstOrDefaultAsync(ct);
            if (agg is null) return;

            var count = agg.Count;
            double? best = agg.Best;
            var passed = agg.Passed > 0;
            var enrollmentId = att.EnrollmentId;

            if (db.Database.IsRelational())
            {
                var q = db.RemedialTrackAddendumProgresses.Where(x => x.AddendumId == addendumId && x.EnrollmentId == enrollmentId);
                if (passed)
                {
                    await q.ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.AttemptsCount, count)
                        .SetProperty(x => x.BestScorePercent, best)
                        .SetProperty(x => x.ExamPassed, true)
                        .SetProperty(x => x.CompletedAtUtc, x => x.CompletedAtUtc ?? now), ct);
                }
                else
                {
                    await q.ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.AttemptsCount, count)
                        .SetProperty(x => x.BestScorePercent, best), ct);
                }
                return;
            }

            // InMemory (الاختبارات) لا يدعم ExecuteUpdate
            var e = await db.RemedialTrackAddendumProgresses.FirstOrDefaultAsync(x => x.AddendumId == addendumId && x.EnrollmentId == enrollmentId, ct);
            if (e is null) return;
            e.AttemptsCount = count;
            e.BestScorePercent = best;
            if (passed)
            {
                e.ExamPassed = true;
                e.CompletedAtUtc ??= now;
            }
            await db.SaveChangesAsync(ct);
        }

        // ═════════════════ مساعدات ═════════════════

        private static RemedialTrackPingResult Ok(RemedialTrackVideoPingResponse body) => new(RemedialTrackPingStatus.Ok, body);

        private static RemedialTrackPingResult Fail(RemedialTrackPingStatus status, string reason, string message) =>
            new(status, new RemedialTrackVideoPingResponse { Ok = false, Reason = reason, Message = message });
    }
}
