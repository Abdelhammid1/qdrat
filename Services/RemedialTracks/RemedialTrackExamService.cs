using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S5: محرك اختبار 101/102 الخفيف (D2). لا يستخدم Exam/ExamAssignment*/ExamResultEngine.
    /// الأسئلة تُجمَّد عند البدء، والمؤقّت من ساعة الخادم، والإجابة الصحيحة لا تغادر الخادم أثناء الحل (D15).
    /// </summary>
    public sealed class RemedialTrackExamService : IRemedialTrackExamService
    {
        public const int SubmitGraceSeconds = 30;   // سماحية شبكة للتسليم بعد انتهاء الوقت

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly IRemedialTrackProgressService _progress;
        private readonly ILogger<RemedialTrackExamService> _logger;

        public RemedialTrackExamService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            IRemedialTrackProgressService progress,
            ILogger<RemedialTrackExamService> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _progress = progress;
            _logger = logger;
        }

        // ═════════════════ لقطة المحاولة (ملكية + بوابة) ═════════════════

        private sealed record AttemptHead(
            int Id,
            int AxisProgressId,
            int EnrollmentId,
            RemedialTrackExamNumber ExamNumber,
            RemedialTrackAttemptStatus Status,
            DateTime ExpiresAtUtc,
            RemedialTrackAxisStatus AxisStatus,
            string AxisTitle,
            string TrackTitle,
            RemedialTrackAccessSnapshot Access);

        private enum Verdict { Ok, NotFound, Forbidden }

        private static Task<AttemptHead?> LoadHeadAsync(ApplicationDbContext db, int studentId, int attemptId, CancellationToken ct) =>
            db.RemedialTrackExamAttempts.AsNoTracking()
                .Where(a => a.Id == attemptId && a.AxisProgress!.Enrollment!.StudentId == studentId)
                .Select(a => new AttemptHead(
                    a.Id,
                    a.AxisProgressId,
                    a.AxisProgress!.EnrollmentId,
                    a.ExamNumber,
                    a.Status,
                    a.ExpiresAtUtc,
                    a.AxisProgress.Status,
                    a.AxisProgress.Axis!.TitleOverride ?? a.AxisProgress.Axis.Section!.Title,
                    a.AxisProgress.Enrollment!.Publication!.Track!.Title,
                    new RemedialTrackAccessSnapshot(
                        a.AxisProgress.Enrollment.StudentId,
                        a.AxisProgress.Enrollment.Status,
                        a.AxisProgress.Enrollment.Publication.Status,
                        a.AxisProgress.Enrollment.Publication.PublishAtUtc,
                        a.AxisProgress.Enrollment.Publication.Mode,
                        a.AxisProgress.Enrollment.Publication.CodeVersion,
                        a.AxisProgress.Enrollment.VerifiedCodeVersion,
                        a.AxisProgress.Enrollment.CodeLockedUntilUtc,
                        a.AxisProgress.Enrollment.Publication.IsDeleted)))
                .FirstOrDefaultAsync(ct);

        // D7: اختبار قيد التنفيذ يُكمل حتى لو جُدِّد الرقم المرجعي؛ الإلغاء/عدم النشر بعد يمنعان دائمًا.
        private static Verdict Check(AttemptHead head, int studentId, DateTime now) =>
            RemedialTrackAccessService.Decide(head.Access, studentId, now) switch
            {
                RemedialTrackAccessOutcome.Allowed or RemedialTrackAccessOutcome.NeedsCode or RemedialTrackAccessOutcome.CodeLocked => Verdict.Ok,
                RemedialTrackAccessOutcome.NotFound => Verdict.NotFound,
                _ => Verdict.Forbidden
            };

        private static int Remaining(DateTime expiresAtUtc, DateTime now) =>
            (int)Math.Max(0, Math.Ceiling((expiresAtUtc - now).TotalSeconds));

        // ═════════════════ RTK-S5.2: بدء الاختبار ═════════════════

        public async Task<RemedialTrackExamStartResult> StartExamAsync(int studentId, int enrollmentId, int axisProgressId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var ap = await db.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(a => a.Id == axisProgressId && a.EnrollmentId == enrollmentId && a.Enrollment!.StudentId == studentId)
                .Select(a => new
                {
                    a.Id,
                    a.AxisId,
                    a.Status,
                    Exam101ModelId = a.Axis!.Exam101ModelId,
                    Exam102ModelId = a.Axis.Exam102ModelId,
                    Minutes = a.Axis.ExamDurationMinutes,
                    Title = a.Axis.TitleOverride ?? a.Axis.Section!.Title,
                    Access = new RemedialTrackAccessSnapshot(
                        a.Enrollment!.StudentId,
                        a.Enrollment.Status,
                        a.Enrollment.Publication!.Status,
                        a.Enrollment.Publication.PublishAtUtc,
                        a.Enrollment.Publication.Mode,
                        a.Enrollment.Publication.CodeVersion,
                        a.Enrollment.VerifiedCodeVersion,
                        a.Enrollment.CodeLockedUntilUtc,
                        a.Enrollment.Publication.IsDeleted)
                })
                .FirstOrDefaultAsync(ct);

            if (ap is null) return new(RemedialTrackExamStartStatus.NotFound);

            // 1) بوابة الوصول — بدء اختبار جديد يتطلب الرقم الحالي (D7)
            switch (RemedialTrackAccessService.Decide(ap.Access, studentId, now))
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

            // 2) الحالة
            var number = ap.Status switch
            {
                RemedialTrackAxisStatus.AwaitingExam101 => RemedialTrackExamNumber.Exam101,
                RemedialTrackAxisStatus.AwaitingExam102 => RemedialTrackExamNumber.Exam102,
                _ => (RemedialTrackExamNumber?)null
            };
            if (number is null)
                return new(RemedialTrackExamStartStatus.Conflict, 0, "الاختبار غير متاح قبل إنهاء فيديوهات المحور.");

            // 3) محاولة موجودة (لا اختبار ثالث ولا مكرر): استئناف أو نتيجة
            var existingId = await FindAttemptIdAsync(db, ap.Id, number.Value, ct);
            if (existingId.HasValue)
                return new(RemedialTrackExamStartStatus.Existing, existingId.Value);

            // 4) إنشاء المحاولة بأسئلة مجمّدة (AddRange لرسم كائنات واحد + SaveChanges واحد)
            var modelId = number == RemedialTrackExamNumber.Exam101 ? ap.Exam101ModelId : ap.Exam102ModelId;
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
                _logger.LogWarning("RTK exam model {ModelId} has no questions (axisProgress {AxisProgressId})", modelId, ap.Id);
                return new(RemedialTrackExamStartStatus.NoQuestions, 0, "اختبار هذا المحور غير جاهز بعد. تواصل مع الإدارة.");
            }

            var attempt = new RemedialTrackExamAttempt
            {
                AxisProgressId = ap.Id,
                ExamNumber = number.Value,
                ModelId = modelId,
                Status = RemedialTrackAttemptStatus.InProgress,
                StartedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(Math.Clamp(ap.Minutes, 1, 600)),
                TotalQuestions = questionIds.Count,
                Questions = questionIds
                    .Select((id, i) => new RemedialTrackExamAttemptQuestion { QuestionId = id, Order = i + 1 })
                    .ToList()
            };
            db.RemedialTrackExamAttempts.Add(attempt);
            db.RemedialTrackEvents.Add(new RemedialTrackEvent
            {
                EnrollmentId = enrollmentId,
                AxisId = ap.AxisId,
                Type = RemedialTrackEventType.ExamStarted,
                Message = $"بدأ الطالب اختبار {(int)number.Value} للمحور «{ap.Title}».",
                CreatedAtUtc = now
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // فريد (AxisProgressId, ExamNumber): طلب متزامن أنشأها — أعد توجيه الطالب لمحاولته
                _logger.LogInformation(ex, "RTK exam attempt created concurrently (axisProgress {AxisProgressId})", ap.Id);
                db.ChangeTracker.Clear();
                var raced = await FindAttemptIdAsync(db, ap.Id, number.Value, ct);
                if (raced.HasValue) return new(RemedialTrackExamStartStatus.Existing, raced.Value);
                throw;
            }

            return new(RemedialTrackExamStartStatus.Created, attempt.Id);
        }

        private static async Task<int?> FindAttemptIdAsync(ApplicationDbContext db, int axisProgressId, RemedialTrackExamNumber number, CancellationToken ct)
        {
            var id = await db.RemedialTrackExamAttempts.AsNoTracking()
                .Where(a => a.AxisProgressId == axisProgressId && a.ExamNumber == number)
                .Select(a => a.Id)
                .FirstOrDefaultAsync(ct);
            return id == 0 ? null : id;
        }

        // ═════════════════ RTK-S5.2: واجهة الحل ═════════════════

        public async Task<RemedialTrackSolveResult> GetSolveAsync(int studentId, int attemptId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var head = await LoadHeadAsync(db, studentId, attemptId, ct);
            if (head is null) return new(RemedialTrackSolveStatus.NotFound);

            switch (Check(head, studentId, now))
            {
                case Verdict.NotFound: return new(RemedialTrackSolveStatus.NotFound);
                case Verdict.Forbidden: return new(RemedialTrackSolveStatus.Forbidden);
            }

            if (head.Status != RemedialTrackAttemptStatus.InProgress)
                return new(RemedialTrackSolveStatus.Closed);

            // انتهى الوقت وسماحية التسليم: صحّح بما أُجيب (Expired) ثم انتقل
            if (now > head.ExpiresAtUtc.AddSeconds(SubmitGraceSeconds))
            {
                await ScoreAndCloseAsync(attemptId, RemedialTrackAttemptStatus.Expired, now, ct);
                await _progress.OnExamSubmittedAsync(attemptId, ct);
                return new(RemedialTrackSolveStatus.Closed);
            }

            var rows = await db.RemedialTrackExamAttemptQuestions.AsNoTracking()
                .Where(x => x.AttemptId == attemptId)
                .OrderBy(x => x.Order)
                .Include(x => x.Question).ThenInclude(q => q!.Options)
                .AsSplitQuery()
                .ToListAsync(ct);

            var questions = new List<StudentRemedialTrackSolveQuestionVm>(rows.Count);
            foreach (var r in rows)
            {
                if (r.Question is null) continue;
                r.Question.Options = r.Question.Options.OrderBy(o => o.Id).ToList();
                var dm = SanitizeForSolving(r.Question.ToDisplayModel());
                dm.SelectedAnswer = r.SelectedAnswer;
                questions.Add(new StudentRemedialTrackSolveQuestionVm
                {
                    Index = r.Order,
                    QuestionId = r.QuestionId,
                    Question = dm,
                    SelectedAnswer = r.SelectedAnswer
                });
            }

            return new(RemedialTrackSolveStatus.Ok, new StudentRemedialTrackSolveVm
            {
                AttemptId = head.Id,
                EnrollmentId = head.EnrollmentId,
                TrackTitle = head.TrackTitle,
                AxisTitle = head.AxisTitle,
                ExamNumber = head.ExamNumber,
                RemainingSeconds = Remaining(head.ExpiresAtUtc, now),
                Questions = questions
            });
        }

        /// <summary>D15: ToDisplayModel() يملأ CorrectAnswer/Explanation/VideoUrl — نصفّرها قبل أن يراها المتصفح.</summary>
        public static QdratNew.ViewModels.Question.QuestionDisplayViewModel SanitizeForSolving(
            QdratNew.ViewModels.Question.QuestionDisplayViewModel dm)
        {
            dm.CorrectAnswer = null;
            dm.Explanation = null;
            dm.VideoUrl = null;
            dm.Hint = null;
            dm.IsAnswerConfirmed = false;
            dm.SelectedCorrectIndex = null;
            return dm;
        }

        // ═════════════════ RTK-S5.2: حفظ الإجابة ═════════════════

        public async Task<RemedialTrackSaveAnswerResult> SaveAnswerAsync(
            int studentId, int attemptId, Guid questionId, string? answer, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var head = await LoadHeadAsync(db, studentId, attemptId, ct);
            if (head is null) return new(RemedialTrackSaveAnswerStatus.NotFound);

            switch (Check(head, studentId, now))
            {
                case Verdict.NotFound: return new(RemedialTrackSaveAnswerStatus.NotFound);
                case Verdict.Forbidden: return new(RemedialTrackSaveAnswerStatus.Forbidden);
            }

            if (head.Status != RemedialTrackAttemptStatus.InProgress || now > head.ExpiresAtUtc)
                return new(RemedialTrackSaveAnswerStatus.Expired, 0, "انتهى وقت الاختبار.");

            if (string.IsNullOrWhiteSpace(answer))
                return new(RemedialTrackSaveAnswerStatus.BadRequest, 0, "الإجابة مطلوبة.");

            var rowId = await db.RemedialTrackExamAttemptQuestions.AsNoTracking()
                .Where(x => x.AttemptId == attemptId && x.QuestionId == questionId)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(ct);
            if (rowId == 0) return new(RemedialTrackSaveAnswerStatus.NotFound);

            // الإجابة يجب أن تطابق أحد خيارات السؤال (نفس قاعدة المقارنة في التصحيح) — نخزّن نص الخيار كما هو
            var optionTexts = await db.QuestionOptions.AsNoTracking()
                .Where(o => o.QuestionId == questionId)
                .Select(o => o.Text)
                .ToListAsync(ct);
            // المتصفح يوحّد CRLF إلى LF في قيمة الخيار؛ نوحّد الطرفين قبل المطابقة (المخزَّن يبقى نص الخيار الأصلي)
            var normalizedAnswer = answer.Replace("\r\n", "\n");
            var matched = optionTexts.FirstOrDefault(t =>
                RemedialTrackScoring.IsCorrect(normalizedAnswer, t?.Replace("\r\n", "\n")));
            if (matched is null)
                return new(RemedialTrackSaveAnswerStatus.BadRequest, 0, "الإجابة ليست ضمن خيارات السؤال.");

            await WriteAnswerAsync(db, rowId, matched, now, ct);
            return new(RemedialTrackSaveAnswerStatus.Ok, Remaining(head.ExpiresAtUtc, now));
        }

        private static async Task WriteAnswerAsync(ApplicationDbContext db, int rowId, string answer, DateTime now, CancellationToken ct)
        {
            if (db.Database.IsRelational())
            {
                // تحديث ذرّي واحد؛ شرط InProgress يمنع الكتابة بعد تسليم متزامن
                await db.RemedialTrackExamAttemptQuestions
                    .Where(x => x.Id == rowId && x.Attempt!.Status == RemedialTrackAttemptStatus.InProgress)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.SelectedAnswer, answer)
                        .SetProperty(x => x.AnsweredAtUtc, (DateTime?)now), ct);
                return;
            }

            // InMemory (الاختبارات) لا يدعم ExecuteUpdate
            var e = await db.RemedialTrackExamAttemptQuestions.FirstAsync(x => x.Id == rowId, ct);
            e.SelectedAnswer = answer;
            e.AnsweredAtUtc = now;
            await db.SaveChangesAsync(ct);
        }

        // ═════════════════ RTK-S5.3: التسليم والتصحيح ═════════════════

        public async Task<RemedialTrackSubmitResult> SubmitAsync(int studentId, int attemptId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var head = await LoadHeadAsync(db, studentId, attemptId, ct);
            if (head is null) return new(RemedialTrackSubmitStatus.NotFound);

            switch (Check(head, studentId, now))
            {
                case Verdict.NotFound: return new(RemedialTrackSubmitStatus.NotFound);
                case Verdict.Forbidden: return new(RemedialTrackSubmitStatus.Forbidden);
            }

            var alreadyClosed = head.Status != RemedialTrackAttemptStatus.InProgress;
            if (!alreadyClosed)
            {
                // مسموح حتى ExpiresAtUtc + 30ث؛ بعدها Expired ويُصحَّح بما أُجيب
                var status = now > head.ExpiresAtUtc.AddSeconds(SubmitGraceSeconds)
                    ? RemedialTrackAttemptStatus.Expired
                    : RemedialTrackAttemptStatus.Submitted;
                await ScoreAndCloseAsync(attemptId, status, now, ct);
            }

            // دائمًا (Idempotent): يُصلح حالة انقطاع بين حفظ التصحيح والانتقال
            await _progress.OnExamSubmittedAsync(attemptId, ct);
            return new(alreadyClosed ? RemedialTrackSubmitStatus.AlreadyClosed : RemedialTrackSubmitStatus.Submitted);
        }

        // يصحّح ويغلق المحاولة بـ SaveChanges واحد (لا حلقة استعلامات). لا أثر إن لم تعد InProgress.
        private async Task ScoreAndCloseAsync(int attemptId, RemedialTrackAttemptStatus closeStatus, DateTime now, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var attempt = await db.RemedialTrackExamAttempts.FirstOrDefaultAsync(a => a.Id == attemptId, ct);
            if (attempt is null || attempt.Status != RemedialTrackAttemptStatus.InProgress) return;

            var passPercent = await db.RemedialTrackExamAttempts.AsNoTracking()
                .Where(a => a.Id == attemptId)
                .Select(a => a.AxisProgress!.Enrollment!.Publication!.Track!.PassPercent)
                .FirstAsync(ct);

            var rows = await db.RemedialTrackExamAttemptQuestions
                .Where(x => x.AttemptId == attemptId)
                .ToListAsync(ct);

            var correctAnswers = (await db.RemedialTrackExamAttemptQuestions.AsNoTracking()
                    .Where(x => x.AttemptId == attemptId)
                    .Select(x => new { x.Id, Correct = x.Question!.CorrectAnswer })
                    .ToListAsync(ct))
                .ToDictionary(x => x.Id, x => x.Correct);

            var correctCount = 0;
            foreach (var row in rows)
            {
                correctAnswers.TryGetValue(row.Id, out var correct);
                if (string.IsNullOrWhiteSpace(correct))
                    _logger.LogError("RTK question {QuestionId} in attempt {AttemptId} has no correct answer — counted as incorrect", row.QuestionId, attemptId);

                row.IsCorrect = RemedialTrackScoring.IsCorrect(row.SelectedAnswer, correct);
                if (row.IsCorrect == true) correctCount++;
            }

            attempt.Status = closeStatus;
            attempt.SubmittedAtUtc = now;
            attempt.TotalQuestions = rows.Count;
            attempt.CorrectCount = correctCount;
            attempt.ScorePercent = RemedialTrackScoring.ScorePercent(correctCount, rows.Count);
            attempt.IsPassed = RemedialTrackScoring.IsPassed(attempt.ScorePercent, passPercent);

            await db.SaveChangesAsync(ct);
        }

        // ═════════════════ RTK-S5.3: صفحة النتيجة ═════════════════

        public async Task<RemedialTrackAttemptResult> GetResultAsync(int studentId, int attemptId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var now = _time.GetUtcNow().UtcDateTime;

            var head = await LoadHeadAsync(db, studentId, attemptId, ct);
            if (head is null) return new(RemedialTrackResultStatus.NotFound);

            switch (Check(head, studentId, now))
            {
                case Verdict.NotFound: return new(RemedialTrackResultStatus.NotFound);
                case Verdict.Forbidden: return new(RemedialTrackResultStatus.Forbidden);
            }

            if (head.Status == RemedialTrackAttemptStatus.InProgress)
            {
                // انتهى الوقت وسماحيته بلا تسليم: أغلقها الآن
                if (now <= head.ExpiresAtUtc.AddSeconds(SubmitGraceSeconds))
                    return new(RemedialTrackResultStatus.InProgress);
                await ScoreAndCloseAsync(attemptId, RemedialTrackAttemptStatus.Expired, now, ct);
            }

            // إصلاح ذاتي: محاولة مُغلقة لكن المحور ما زال ينتظر نفس الاختبار (انقطاع قبل الانتقال)
            var awaitingThis = head.ExamNumber == RemedialTrackExamNumber.Exam101
                ? RemedialTrackAxisStatus.AwaitingExam101
                : RemedialTrackAxisStatus.AwaitingExam102;
            if (head.AxisStatus == awaitingThis || head.Status == RemedialTrackAttemptStatus.InProgress)
                await _progress.OnExamSubmittedAsync(attemptId, ct);

            var att = await db.RemedialTrackExamAttempts.AsNoTracking()
                .Where(a => a.Id == attemptId)
                .Select(a => new
                {
                    a.Status,
                    a.ScorePercent,
                    a.IsPassed,
                    a.CorrectCount,
                    a.TotalQuestions,
                    a.AxisProgress!.Order,
                    PassPercent = a.AxisProgress.Enrollment!.Publication!.Track!.PassPercent
                })
                .FirstAsync(ct);

            var next = await db.RemedialTrackAxisProgresses.AsNoTracking()
                .Where(a => a.EnrollmentId == head.EnrollmentId && a.Order > att.Order)
                .OrderBy(a => a.Order)
                .Select(a => new { a.Id, Title = a.Axis!.TitleOverride ?? a.Axis.Section!.Title })
                .FirstOrDefaultAsync(ct);

            var nextStep = att.IsPassed
                ? (next is null ? RemedialTrackResultNext.FinalReport : RemedialTrackResultNext.NextAxis)
                : head.ExamNumber == RemedialTrackExamNumber.Exam101
                    ? RemedialTrackResultNext.RewatchThenExam102
                    : (next is null ? RemedialTrackResultNext.AwaitAdminFinal : RemedialTrackResultNext.AwaitAdmin);

            return new(RemedialTrackResultStatus.Ok, new StudentRemedialTrackResultVm
            {
                AttemptId = head.Id,
                EnrollmentId = head.EnrollmentId,
                AxisProgressId = head.AxisProgressId,
                TrackTitle = head.TrackTitle,
                AxisTitle = head.AxisTitle,
                ExamNumber = head.ExamNumber,
                ScorePercent = att.ScorePercent,
                IsPassed = att.IsPassed,
                TimedOut = att.Status == RemedialTrackAttemptStatus.Expired,
                CorrectCount = att.CorrectCount,
                TotalQuestions = att.TotalQuestions,
                PassPercent = att.PassPercent,
                Next = nextStep,
                NextAxisProgressId = next?.Id,
                NextAxisTitle = next?.Title,
                // نبرة محايدة بلا لغة رسوب (قاعدة المشروع): الحد = نسبة اجتياز الخطة
                Tone = ResultToneHelper.Build(att.ScorePercent, ResultContext.Exam, att.PassPercent)
            });
        }
    }
}
