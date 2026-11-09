using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Services.OnlineLecture
{
    public enum LecturePingStatus
    {
        Ok = 0,
        BadRequest = 1,
        /// <summary>المفتاح مغلق أو R2 غير متحقق أو العنصر لا يتبع المحتوى — 404 بلا تمييز.</summary>
        NotFound = 2,
        /// <summary>R13/R14: مقفلة (reason = "locked").</summary>
        Conflict = 3
    }

    public sealed record LecturePingResult(LecturePingStatus Status, StudentLecturePingResponse Body);

    /// <summary>
    /// OLC-S7.1 — نبضة المشاهدة (C.7-P5/P7/P10/P11) بنفس ترتيب <c>RemedialTrackProgressService.RecordPingAsync</c>:
    /// 1) حالة صالحة ← 2/3) الملكية + المفتاح + R2 (<see cref="IOnlineLearningAccessService.CanViewAsync"/>) ←
    /// 4) R13 (نفس حساب القائمة) و R14 (لا عنصر أسبق غير مكتمل) ← 5) المكتمل ⇒ رد idempotent ←
    /// 6) <see cref="LectureProgressCalculator"/> ← 7) إنشاء الصف أول مرة أو تحديث ذرّي بشرط <c>LastPingAtUtc == expected</c> ←
    /// 8) الرد بعقد C.5. studentId من سياق الطالب في الخادم فقط.
    /// </summary>
    public interface IStudentLectureProgressService
    {
        Task<LecturePingResult> RecordPingAsync(int studentId, StudentLecturePingRequest request, CancellationToken ct = default);
    }

    public sealed class StudentLectureProgressService : IStudentLectureProgressService
    {
        public const string LockedLectureMessage = "أكمل المحاضرة السابقة أولًا.";
        public const string LockedItemMessage = "أكمل الفيديو السابق أولًا.";
        public const string InsufficientMessage = "لم تكتمل المشاهدة المطلوبة، أعد المشاهدة من البداية.";
        public const string NotFoundMessage = "هذه المحاضرة غير متاحة حاليًا.";

        private readonly ApplicationDbContext _db;
        private readonly IOnlineLearningAccessService _access;
        private readonly ILectureSequenceService _sequence;
        private readonly TimeProvider _time;
        private readonly ILogger<StudentLectureProgressService> _logger;

        public StudentLectureProgressService(
            ApplicationDbContext db,
            IOnlineLearningAccessService access,
            ILectureSequenceService sequence,
            TimeProvider time,
            ILogger<StudentLectureProgressService> logger)
        {
            _db = db;
            _access = access;
            _sequence = sequence;
            _time = time;
            _logger = logger;
        }

        public async Task<LecturePingResult> RecordPingAsync(int studentId, StudentLecturePingRequest request, CancellationToken ct = default)
        {
            // 1) حالة صالحة + شكل الطلب
            var state = request?.State?.Trim().ToLowerInvariant();
            if (request is null || state is not ("playing" or "paused" or "ended") || request.LectureContentId <= 0 || request.ItemId is <= 0)
                return Bad("حالة المشاهدة غير صالحة.");

            // 2/3) الملكية + المفتاح + R2 في استعلام واحد — أي شرط ناقص = notfound
            var access = await _access.CanViewAsync(studentId, request.LectureContentId, ct);
            if (access is null)
                return Missing();

            // 4) R13 على الخادم: نفس حساب القائمة وصفحة المشاهدة
            var sequence = await _sequence.GetForStudentAsync(studentId, access.BatchId, ct);
            var index = -1;
            for (var i = 0; i < sequence.Count; i++)
            {
                if (sequence[i].LectureContentId == request.LectureContentId) { index = i; break; }
            }
            if (index < 0)
                return Missing();

            var row = sequence[index];
            if (!row.CanOpen)
                return Locked(LockedLectureMessage);

            var next = index + 1 < sequence.Count && sequence[index + 1].CurriculumId == row.CurriculumId ? sequence[index + 1] : null;

            var content = await _db.LectureContents.AsNoTracking()
                .Where(c => c.Id == request.LectureContentId)
                .Select(c => new { c.ContentMode, c.DurationSeconds })
                .FirstOrDefaultAsync(ct);
            if (content is null)
                return Missing();

            var now = _time.GetUtcNow().UtcDateTime;

            if (content.ContentMode == LectureContentMode.FullVideo)
            {
                if (request.ItemId is not null)
                    return Bad("هذه المحاضرة فيديو واحد بلا قائمة تشغيل.");

                if (content.DurationSeconds is not > 0)
                    return NoAdminDuration(request.LectureContentId, null);

                var target = new FullTarget(_db, request.LectureContentId, studentId);
                var outcome = await ApplyAsync(target, state, request, content.DurationSeconds, now, ct);
                if (outcome is null)
                    return Missing();

                // FullVideo: إتمام الفيديو = إتمام المحاضرة (R13)
                var lectureCompleted = outcome.Completed;
                return Ok(outcome, lectureCompleted, null, NextLectureUnlocked(lectureCompleted, row, next));
            }

            // ─── IndicatorVideos ───
            if (request.ItemId is null)
                return Bad("حدّد الفيديو الجاري.");

            var itemId = request.ItemId.Value;
            var item = await _db.LectureContentItems.AsNoTracking()   // QueryFilter: العنصر المحذوف = notfound
                .Where(i => i.Id == itemId && i.LectureContentId == request.LectureContentId)
                .Select(i => new { i.Id, i.SortOrder, i.DurationSeconds })
                .FirstOrDefaultAsync(ct);
            if (item is null)
                return Missing();

            if (item.DurationSeconds is not > 0)
                return NoAdminDuration(request.LectureContentId, itemId);

            // R14: كل عنصر نشط أسبق (SortOrder ثم Id — نفس ترتيب صفحة المشاهدة) يجب أن يكون مكتملًا. المكتمل يُعاد idempotent.
            if (!row.IsCompleted)
            {
                var pendingBefore = await _db.LectureContentItems.AsNoTracking()
                    .AnyAsync(i => i.LectureContentId == request.LectureContentId
                                   && (i.SortOrder < item.SortOrder || (i.SortOrder == item.SortOrder && i.Id < item.Id))
                                   && !_db.LectureContentItemProgress.Any(p => p.LectureContentItemId == i.Id
                                                                              && p.StudentId == studentId && p.IsCompleted), ct);
                if (pendingBefore)
                    return Locked(LockedItemMessage);
            }

            var itemOutcome = await ApplyAsync(new ItemTarget(_db, itemId, studentId), state, request, item.DurationSeconds, now, ct);
            if (itemOutcome is null)
                return Missing();

            int? nextItemId = null;
            var itemsDone = row.IsCompleted;
            if (itemOutcome.Completed && !itemsDone)
            {
                nextItemId = await _db.LectureContentItems.AsNoTracking()
                    .Where(i => i.LectureContentId == request.LectureContentId
                                && (i.SortOrder > item.SortOrder || (i.SortOrder == item.SortOrder && i.Id > item.Id)))
                    .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                    .Select(i => (int?)i.Id)
                    .FirstOrDefaultAsync(ct);

                // R13: «أتمّ المحاضرة» = كل العناصر النشطة مكتملة
                itemsDone = !await _db.LectureContentItems.AsNoTracking()
                    .AnyAsync(i => i.LectureContentId == request.LectureContentId
                                   && !_db.LectureContentItemProgress.Any(p => p.LectureContentItemId == i.Id
                                                                              && p.StudentId == studentId && p.IsCompleted), ct);
            }

            var completedLecture = itemOutcome.Completed && itemsDone;
            return Ok(itemOutcome, completedLecture, completedLecture ? null : nextItemId, NextLectureUnlocked(completedLecture, row, next));
        }

        // المحاضرة التالية في نفس المنهج تُفتح متى أتمّ الطالب هذه وهي غير مقفلة (كل ما قبلها مكتمل).
        // المكتملة التي صارت مقفلة بنشر محاضرة أقدم لاحقًا: القرار من التسلسل المحسوب كما هو.
        private static bool NextLectureUnlocked(bool lectureCompleted, StudentSequenceLecture row, StudentSequenceLecture? next)
            => lectureCompleted && next is not null && (next.CanOpen || !row.IsLocked);

        // ═════════════════ التطبيق الذرّي (5 → 7) ═════════════════

        private sealed record ProgressSnapshot(
            int Id, double WatchedSeconds, double MaxPositionSeconds, int? DurationSeconds,
            string? LastPingState, DateTime? LastPingAtUtc, bool EndedSeen, bool IsCompleted);

        private sealed record PingOutcome(double WatchedSeconds, double RequiredSeconds, bool Ended, bool Completed);

        /// <summary>
        /// null = الصف لم يُقرأ بعد إعادة المحاولة (لا يحدث عمليًا). مكتمل سابقًا ⇒ idempotent بلا كتابة (P11).
        /// أول نبضة تُنشئ الصف؛ تكرار المفتاح الفريد (طلبان متزامنان) ⇒ إعادة القراءة مرة واحدة.
        /// التحديث بشرط <c>!IsCompleted &amp;&amp; LastPingAtUtc == expected</c>: الطلب المتزامن الخاسر يُعاد تقييمه مرة واحدة فلا يتضاعف الرصيد.
        /// </summary>
        private async Task<PingOutcome?> ApplyAsync(IProgressTarget target, string state, StudentLecturePingRequest request,
            int? adminDuration, DateTime now, CancellationToken ct)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var snap = await target.ReadAsync(ct);
                if (snap is { IsCompleted: true })
                    return Done(snap, adminDuration);

                var c = LectureProgressCalculator.Compute(
                    snap?.WatchedSeconds ?? 0, snap?.EndedSeen ?? false, snap?.LastPingState, snap?.LastPingAtUtc,
                    now, state, adminDuration, snap?.DurationSeconds, request.Duration,
                    request.Position, snap?.MaxPositionSeconds ?? 0);

                var written = snap is null
                    ? await target.InsertAsync(c, state, now, ct)
                    : await target.UpdateAsync(snap.Id, snap.LastPingAtUtc, c, state, now, ct) > 0;

                if (written)
                    return new PingOutcome(c.WatchedSeconds, c.RequiredSeconds, c.Ended, c.Completed);

                _logger.LogDebug("OLC ping lost a concurrent write ({Target}); re-reading once", target);
            }

            // طلب متزامن سبقنا مرتين: نرد بالحقيقة المخزّنة بلا رصيد جديد
            var last = await target.ReadAsync(ct);
            if (last is null)
                return null;
            if (last.IsCompleted)
                return Done(last, adminDuration);

            var duration = adminDuration is > 0 ? adminDuration.Value : last.DurationSeconds ?? 0;
            return new PingOutcome(last.WatchedSeconds, LectureWatchRules.RequiredSeconds(duration), last.EndedSeen, false);
        }

        private static PingOutcome Done(ProgressSnapshot snap, int? adminDuration)
            => new(snap.WatchedSeconds, LectureWatchRules.RequiredSeconds(adminDuration is > 0 ? adminDuration : snap.DurationSeconds), true, true);

        private interface IProgressTarget
        {
            Task<ProgressSnapshot?> ReadAsync(CancellationToken ct);
            Task<bool> InsertAsync(LectureProgressComputation c, string state, DateTime now, CancellationToken ct);
            Task<int> UpdateAsync(int id, DateTime? expected, LectureProgressComputation c, string state, DateTime now, CancellationToken ct);
        }

        private sealed class FullTarget : IProgressTarget
        {
            private readonly ApplicationDbContext _db;
            private readonly int _contentId, _studentId;

            public FullTarget(ApplicationDbContext db, int contentId, int studentId)
            {
                _db = db; _contentId = contentId; _studentId = studentId;
            }

            public override string ToString() => $"content {_contentId}";

            public Task<ProgressSnapshot?> ReadAsync(CancellationToken ct)
                => _db.LectureContentProgress.AsNoTracking()
                    .Where(p => p.LectureContentId == _contentId && p.StudentId == _studentId)
                    .Select(p => new ProgressSnapshot(p.Id, p.WatchedSeconds, p.MaxPositionSeconds, p.DurationSeconds,
                        p.LastPingState, p.LastPingAtUtc, p.EndedSeen, p.IsCompleted))
                    .FirstOrDefaultAsync(ct);

            public async Task<bool> InsertAsync(LectureProgressComputation c, string state, DateTime now, CancellationToken ct)
            {
                var row = new LectureContentProgress
                {
                    LectureContentId = _contentId,
                    StudentId = _studentId,
                    WatchedSeconds = c.WatchedSeconds,
                    MaxPositionSeconds = c.MaxPositionSeconds,
                    DurationSeconds = (int)Math.Ceiling(c.DurationSeconds),
                    LastPingState = state,
                    FirstPingAtUtc = now,
                    LastPingAtUtc = now,
                    EndedSeen = c.Ended,
                    IsCompleted = c.Completed,
                    CompletedAtUtc = c.Completed ? now : null,
                    CreatedAt = now
                };
                return await TryInsertAsync(_db, row, ct);
            }

            public async Task<int> UpdateAsync(int id, DateTime? expected, LectureProgressComputation c, string state, DateTime now, CancellationToken ct)
            {
                var watched = c.WatchedSeconds;
                var maxPos = c.MaxPositionSeconds;
                var duration = (int?)(int)Math.Ceiling(c.DurationSeconds);
                var ended = c.Ended;
                var completed = c.Completed;
                DateTime? completedAt = completed ? now : null;

                if (_db.Database.IsRelational())
                {
                    return await _db.LectureContentProgress
                        .Where(x => x.Id == id && !x.IsCompleted && x.LastPingAtUtc == expected)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(x => x.WatchedSeconds, watched)
                            .SetProperty(x => x.MaxPositionSeconds, maxPos)
                            .SetProperty(x => x.DurationSeconds, duration)
                            .SetProperty(x => x.EndedSeen, ended)
                            .SetProperty(x => x.LastPingAtUtc, (DateTime?)now)
                            .SetProperty(x => x.LastPingState, state)
                            .SetProperty(x => x.FirstPingAtUtc, x => x.FirstPingAtUtc ?? now)
                            .SetProperty(x => x.IsCompleted, completed)
                            .SetProperty(x => x.CompletedAtUtc, completedAt)
                            .SetProperty(x => x.UpdatedAt, (DateTime?)now), ct);
                }

                // InMemory (الاختبارات) لا يدعم ExecuteUpdate
                var e = await _db.LectureContentProgress.FirstOrDefaultAsync(x => x.Id == id && !x.IsCompleted && x.LastPingAtUtc == expected, ct);
                if (e is null) return 0;
                e.WatchedSeconds = watched;
                e.MaxPositionSeconds = maxPos;
                e.DurationSeconds = duration;
                e.EndedSeen = ended;
                e.LastPingAtUtc = now;
                e.LastPingState = state;
                e.FirstPingAtUtc ??= now;
                e.IsCompleted = completed;
                e.CompletedAtUtc = completedAt;
                e.UpdatedAt = now;
                await _db.SaveChangesAsync(ct);
                _db.Entry(e).State = EntityState.Detached;
                return 1;
            }
        }

        private sealed class ItemTarget : IProgressTarget
        {
            private readonly ApplicationDbContext _db;
            private readonly int _itemId, _studentId;

            public ItemTarget(ApplicationDbContext db, int itemId, int studentId)
            {
                _db = db; _itemId = itemId; _studentId = studentId;
            }

            public override string ToString() => $"item {_itemId}";

            public Task<ProgressSnapshot?> ReadAsync(CancellationToken ct)
                => _db.LectureContentItemProgress.AsNoTracking()
                    .Where(p => p.LectureContentItemId == _itemId && p.StudentId == _studentId)
                    .Select(p => new ProgressSnapshot(p.Id, p.WatchedSeconds, p.MaxPositionSeconds, p.DurationSeconds,
                        p.LastPingState, p.LastPingAtUtc, p.EndedSeen, p.IsCompleted))
                    .FirstOrDefaultAsync(ct);

            public async Task<bool> InsertAsync(LectureProgressComputation c, string state, DateTime now, CancellationToken ct)
            {
                var row = new LectureContentItemProgress
                {
                    LectureContentItemId = _itemId,
                    StudentId = _studentId,
                    WatchedSeconds = c.WatchedSeconds,
                    MaxPositionSeconds = c.MaxPositionSeconds,
                    DurationSeconds = (int)Math.Ceiling(c.DurationSeconds),
                    LastPingState = state,
                    FirstPingAtUtc = now,
                    LastPingAtUtc = now,
                    EndedSeen = c.Ended,
                    IsCompleted = c.Completed,
                    CompletedAtUtc = c.Completed ? now : null,
                    CreatedAt = now
                };
                return await TryInsertAsync(_db, row, ct);
            }

            public async Task<int> UpdateAsync(int id, DateTime? expected, LectureProgressComputation c, string state, DateTime now, CancellationToken ct)
            {
                var watched = c.WatchedSeconds;
                var maxPos = c.MaxPositionSeconds;
                var duration = (int?)(int)Math.Ceiling(c.DurationSeconds);
                var ended = c.Ended;
                var completed = c.Completed;
                DateTime? completedAt = completed ? now : null;

                if (_db.Database.IsRelational())
                {
                    return await _db.LectureContentItemProgress
                        .Where(x => x.Id == id && !x.IsCompleted && x.LastPingAtUtc == expected)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(x => x.WatchedSeconds, watched)
                            .SetProperty(x => x.MaxPositionSeconds, maxPos)
                            .SetProperty(x => x.DurationSeconds, duration)
                            .SetProperty(x => x.EndedSeen, ended)
                            .SetProperty(x => x.LastPingAtUtc, (DateTime?)now)
                            .SetProperty(x => x.LastPingState, state)
                            .SetProperty(x => x.FirstPingAtUtc, x => x.FirstPingAtUtc ?? now)
                            .SetProperty(x => x.IsCompleted, completed)
                            .SetProperty(x => x.CompletedAtUtc, completedAt)
                            .SetProperty(x => x.UpdatedAt, (DateTime?)now), ct);
                }

                var e = await _db.LectureContentItemProgress.FirstOrDefaultAsync(x => x.Id == id && !x.IsCompleted && x.LastPingAtUtc == expected, ct);
                if (e is null) return 0;
                e.WatchedSeconds = watched;
                e.MaxPositionSeconds = maxPos;
                e.DurationSeconds = duration;
                e.EndedSeen = ended;
                e.LastPingAtUtc = now;
                e.LastPingState = state;
                e.FirstPingAtUtc ??= now;
                e.IsCompleted = completed;
                e.CompletedAtUtc = completedAt;
                e.UpdatedAt = now;
                await _db.SaveChangesAsync(ct);
                _db.Entry(e).State = EntityState.Detached;
                return 1;
            }
        }

        // أول نبضة: إنشاء الصف. تكرار المفتاح الفريد (LectureContentId|ItemId, StudentId) من طلب متزامن ⇒ false (يُعاد القراءة)
        private static async Task<bool> TryInsertAsync<T>(ApplicationDbContext db, T row, CancellationToken ct) where T : class
        {
            db.Set<T>().Add(row);
            try
            {
                await db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException)
            {
                return false;
            }
            finally
            {
                db.Entry(row).State = EntityState.Detached;
            }
        }

        // ═════════════════ الردود (عقد C.5) ═════════════════

        private static LecturePingResult Ok(PingOutcome o, bool lectureCompleted, int? nextItemId, bool nextLectureUnlocked)
        {
            var insufficient = !o.Completed && o.Ended;
            return new LecturePingResult(LecturePingStatus.Ok, new StudentLecturePingResponse
            {
                Ok = true,
                Completed = o.Completed,
                Reason = insufficient ? "insufficient" : null,
                Message = insufficient ? InsufficientMessage : null,
                WatchedSeconds = (int)Math.Floor(o.WatchedSeconds),
                RequiredSeconds = (int)Math.Ceiling(o.RequiredSeconds),
                NextItemId = nextItemId,
                LectureCompleted = lectureCompleted,
                NextLectureUnlocked = nextLectureUnlocked
            });
        }

        private static LecturePingResult Bad(string message)
            => new(LecturePingStatus.BadRequest, new StudentLecturePingResponse { Ok = false, Reason = "bad-request", Message = message });

        private static LecturePingResult Missing()
            => new(LecturePingStatus.NotFound, new StudentLecturePingResponse { Ok = false, Reason = "notfound", Message = NotFoundMessage });

        /// <summary>
        /// OLC-S8.1 — بلا مدة من الأدمن لا يُحتسب شيء: مدة المتصفح قابلة للتزوير (duration=5 ثم ended يُتمّ المحاضرة في ثوانٍ
        /// ويفتح التالية). النشر يمنع ذلك؛ هذا حارس للبيانات القديمة. 404 notfound بلا كتابة.
        /// </summary>
        private LecturePingResult NoAdminDuration(int lectureContentId, int? itemId)
        {
            _logger.LogWarning("OLC: ping refused — no admin duration for content {ContentId} item {ItemId}", lectureContentId, itemId);
            return Missing();
        }

        private static LecturePingResult Locked(string message)
            => new(LecturePingStatus.Conflict, new StudentLecturePingResponse { Ok = false, Reason = "locked", Message = message });
    }
}
