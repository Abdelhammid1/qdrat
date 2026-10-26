using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// مراجعة الأسئلة وتقرير المحاولة لاختباري 101/102 بعد إغلاقهما. للقراءة فقط: لا كتابة ولا تغيير حالة.
    /// الملكية والبوابة بنفس قاعدة صفحة النتيجة (محاولة منتهية تُعرض حتى لو جُدِّد الرقم المرجعي).
    /// </summary>
    public sealed class RemedialTrackAttemptReviewService : IRemedialTrackAttemptReviewService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;

        public RemedialTrackAttemptReviewService(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider time)
        {
            _dbFactory = dbFactory;
            _time = time;
        }

        private sealed record Head(
            int Id,
            int EnrollmentId,
            int AxisProgressId,
            RemedialTrackExamNumber ExamNumber,
            RemedialTrackAttemptStatus Status,
            DateTime StartedAtUtc,
            DateTime? SubmittedAtUtc,
            double ScorePercent,
            bool IsPassed,
            int PassPercent,
            int AllowedMinutes,
            string AxisTitle,
            string TrackTitle,
            RemedialTrackAccessSnapshot Access);

        private static Task<Head?> LoadHeadAsync(ApplicationDbContext db, int studentId, int attemptId, CancellationToken ct) =>
            db.RemedialTrackExamAttempts.AsNoTracking()
                // محاولة الملحق خارج النطاق (D32) وغير المملوكة ≡ غير موجودة
                .Where(a => a.Id == attemptId
                            && a.AddendumId == null
                            && a.ExamNumber != RemedialTrackExamNumber.Addendum
                            && a.AxisProgress!.Enrollment!.StudentId == studentId)
                .Select(a => new Head(
                    a.Id,
                    a.AxisProgress!.EnrollmentId,
                    a.AxisProgressId,
                    a.ExamNumber,
                    a.Status,
                    a.StartedAtUtc,
                    a.SubmittedAtUtc,
                    a.ScorePercent,
                    a.IsPassed,
                    a.AxisProgress.Enrollment!.Publication!.Track!.PassPercent,
                    a.ExamNumber == RemedialTrackExamNumber.Exam101
                        ? a.AxisProgress.Axis!.ExamDurationMinutes
                        : a.AxisProgress.Axis!.Exam102DurationMinutes,
                    a.AxisProgress.Axis.TitleOverride ?? a.AxisProgress.Axis.Section!.Title,
                    a.AxisProgress.Enrollment.Publication.Track.Title,
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

        // null = مسموح؛ وإلا الحالة التي يجب إرجاعها
        private RemedialTrackAttemptReviewStatus? Gate(Head? head, int studentId)
        {
            if (head is null) return RemedialTrackAttemptReviewStatus.NotFound;

            switch (RemedialTrackAccessService.Decide(head.Access, studentId, _time.GetUtcNow().UtcDateTime))
            {
                case RemedialTrackAccessOutcome.NotFound:
                    return RemedialTrackAttemptReviewStatus.NotFound;
                case RemedialTrackAccessOutcome.Cancelled:
                case RemedialTrackAccessOutcome.NotYetPublished:
                    return RemedialTrackAttemptReviewStatus.Forbidden;
            }

            // لا كشف للإجابات قبل الإغلاق
            return head.Status == RemedialTrackAttemptStatus.InProgress
                ? RemedialTrackAttemptReviewStatus.InProgress
                : null;
        }

        /// <summary>
        /// مراجعة الإجابات (تكشف الصحيح) تُفتح بعد نهاية الاختبار الثاني؛ ومن اجتاز الأول لا اختبار ثانٍ له فتُفتح فورًا.
        /// الاختبار الأول الراسب يبقى مقفلًا حتى تُغلق محاولة 102 لنفس المحور (منعًا لتسريب إجابات الثاني).
        /// </summary>
        public static bool CanReviewAnswers(RemedialTrackExamNumber number, bool isPassed, bool hasClosedExam102) =>
            number == RemedialTrackExamNumber.Exam102 || isPassed || hasClosedExam102;

        public static Task<bool> HasClosedExam102Async(ApplicationDbContext db, int axisProgressId, CancellationToken ct) =>
            db.RemedialTrackExamAttempts.AsNoTracking()
                .AnyAsync(x => x.AxisProgressId == axisProgressId
                               && x.ExamNumber == RemedialTrackExamNumber.Exam102
                               && x.Status != RemedialTrackAttemptStatus.InProgress, ct);

        private static string FormatDuration(DateTime startUtc, DateTime? endUtc)
        {
            if (endUtc is null) return "غير محدد";
            var seconds = (int)Math.Max(0, (endUtc.Value - startUtc).TotalSeconds);
            return seconds < 60 ? $"{seconds} ثانية" : $"{seconds / 60} دقيقة و{seconds % 60} ثانية";
        }

        // ═════════════════ المراجعة ═════════════════

        public async Task<RemedialTrackAttemptReviewResult> GetReviewAsync(int studentId, int attemptId, string backUrl, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var head = await LoadHeadAsync(db, studentId, attemptId, ct);
            var blocked = Gate(head, studentId);
            if (blocked.HasValue) return new(blocked.Value);

            if (!CanReviewAnswers(head!.ExamNumber, head.IsPassed, await HasClosedExam102Async(db, head.AxisProgressId, ct)))
                return new(RemedialTrackAttemptReviewStatus.ReviewLocked);

            var rows = await db.RemedialTrackExamAttemptQuestions.AsNoTracking()
                .Where(x => x.AttemptId == attemptId)
                .OrderBy(x => x.Order)
                .Include(x => x.Question).ThenInclude(q => q!.Options)
                .Include(x => x.Question).ThenInclude(q => q!.VerbalPassage)
                .Include(x => x.Question).ThenInclude(q => q!.Lesson).ThenInclude(l => l!.Section)
                .Include(x => x.Question).ThenInclude(q => q!.Curriculum)
                .AsSplitQuery()
                .ToListAsync(ct);

            var questions = new List<ExamReviewQuestionVm>(rows.Count);
            foreach (var r in rows)
            {
                var q = r.Question;
                if (q is null) continue;

                var hasAnswer = !string.IsNullOrWhiteSpace(r.SelectedAnswer);
                questions.Add(new ExamReviewQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionOrder = r.Order,
                    QuestionTitle = q.Title ?? string.Empty,
                    StudentAnswer = r.SelectedAnswer ?? string.Empty,
                    CorrectAnswer = q.CorrectAnswer ?? string.Empty,
                    IsCorrect = r.IsCorrect == true,
                    IsSkipped = !hasAnswer,
                    LessonTitle = q.Lesson?.Title ?? string.Empty,
                    SectionTitle = q.Lesson?.Section?.Title ?? string.Empty,
                    IsQuantitative = q.IsQuantitative,
                    Explanation = q.Explanation ?? string.Empty,
                    VideoUrl = q.VideoUrl ?? string.Empty,
                    IsRTL = q.Curriculum?.IsRTL ?? true,
                    TimeTakenSeconds = 0,
                    VerbalPassageTitle = q.VerbalPassage?.Title ?? string.Empty,
                    VerbalPassageContent = q.VerbalPassage?.Content ?? string.Empty,
                    VerbalPassageMediaUrl = q.VerbalPassage?.MediaUrl,
                    VerbalPassageType = q.VerbalPassage != null ? (PassageType?)q.VerbalPassage.Type : null,
                    ImageUrl = q.ImageUrl ?? string.Empty,
                    ComparisonValue1 = q.ValueA ?? string.Empty,
                    ComparisonValue2 = q.ValueB ?? string.Empty,
                    DisplayType = q.Template == QuestionTemplate.CompareValues
                        ? QuestionDisplayType.ComparisonText
                        : q.Template == QuestionTemplate.CompareWithImage
                            ? QuestionDisplayType.ComparisonWithImage
                            : QuestionDisplayType.WithImage,
                    Options = q.Options.OrderBy(o => o.Id).Select(o => new QuestionOptionVm
                    {
                        Text = o.Text ?? string.Empty,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = RemedialTrackScoring.IsCorrect(o.Text, q.CorrectAnswer),
                        IsSelectedByStudent = hasAnswer && RemedialTrackScoring.IsCorrect(r.SelectedAnswer, o.Text)
                    }).ToList()
                });
            }

            var total = questions.Count;
            var correct = questions.Count(x => x.IsCorrect);
            var skipped = questions.Count(x => x.IsSkipped);

            return new(RemedialTrackAttemptReviewStatus.Ok, new ExamReviewViewModel
            {
                ExamAssignmentId = head!.Id,
                ExamTitle = $"{head.ExamNumber.DisplayName()} — {head.AxisTitle}",
                TotalQuestions = total,
                CorrectAnswers = correct,
                WrongAnswers = total - correct - skipped,
                SkippedQuestions = skipped,
                ScorePercentage = head.ScorePercent,
                TimeSpentFormatted = FormatDuration(head.StartedAtUtc, head.SubmittedAtUtc),
                IsRTL = questions.FirstOrDefault()?.IsRTL ?? true,
                Questions = questions,
                BackUrl = backUrl,
                BackText = "العودة إلى النتيجة",
                ShowQuestionTimes = false   // لا يُسجَّل زمن لكل سؤال في محرك 101/102
            });
        }

        // ═════════════════ التقرير ═════════════════

        public async Task<RemedialTrackAttemptReportResult> GetReportAsync(int studentId, int attemptId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var head = await LoadHeadAsync(db, studentId, attemptId, ct);
            var blocked = Gate(head, studentId);
            if (blocked.HasValue) return new(blocked.Value);

            // التقرير يكشف الدروس/الأسئلة الخاطئة فيُفتح بنفس قاعدة مراجعة الإجابات
            if (!CanReviewAnswers(head!.ExamNumber, head.IsPassed, await HasClosedExam102Async(db, head.AxisProgressId, ct)))
                return new(RemedialTrackAttemptReviewStatus.ReviewLocked);

            // إسقاط خفيف (بلا Include): نحتاج الترتيب والنتيجة والدرس/القسم فقط
            var rows = await db.RemedialTrackExamAttemptQuestions.AsNoTracking()
                .Where(x => x.AttemptId == attemptId)
                .OrderBy(x => x.Order)
                .Select(x => new
                {
                    x.Order,
                    x.SelectedAnswer,
                    x.IsCorrect,
                    LessonId = x.Question!.LessonId,
                    LessonTitle = x.Question.Lesson!.Title,
                    SectionId = x.Question.Lesson.SectionId,
                    SectionTitle = x.Question.Lesson.Section!.Title
                })
                .ToListAsync(ct);

            var items = rows.Select(r => new
            {
                r.Order,
                r.LessonId,
                r.LessonTitle,
                r.SectionId,
                r.SectionTitle,
                Skipped = string.IsNullOrWhiteSpace(r.SelectedAnswer),
                Correct = r.IsCorrect == true
            }).ToList();

            var total = items.Count;
            var correct = items.Count(i => i.Correct);
            var skipped = items.Count(i => i.Skipped);

            StudentRemedialTrackAttemptBreakdownVm Build(string title, string? parent, IEnumerable<(bool Correct, bool Skipped)> g)
            {
                var list = g.ToList();
                return new StudentRemedialTrackAttemptBreakdownVm
                {
                    Title = title,
                    ParentTitle = parent,
                    Total = list.Count,
                    Correct = list.Count(x => x.Correct),
                    Skipped = list.Count(x => x.Skipped),
                    Wrong = list.Count(x => !x.Correct && !x.Skipped)
                };
            }

            var sections = items
                .GroupBy(i => new { i.SectionId, i.SectionTitle })
                .Select(g => Build(g.Key.SectionTitle, null, g.Select(i => (i.Correct, i.Skipped))))
                .OrderBy(s => s.AccuracyPercent)
                .ToList();

            var lessons = items
                .GroupBy(i => new { i.LessonId, i.LessonTitle, i.SectionTitle })
                .Select(g => Build(g.Key.LessonTitle, g.Key.SectionTitle, g.Select(i => (i.Correct, i.Skipped))))
                .OrderBy(l => l.AccuracyPercent).ThenByDescending(l => l.Total)
                .ToList();

            var missed = items
                .Where(i => !i.Correct)
                .Select(i => new StudentRemedialTrackAttemptMissedVm { Order = i.Order, LessonTitle = i.LessonTitle, IsSkipped = i.Skipped })
                .ToList();

            return new(RemedialTrackAttemptReviewStatus.Ok, new StudentRemedialTrackAttemptReportVm
            {
                AttemptId = head!.Id,
                EnrollmentId = head.EnrollmentId,
                AxisProgressId = head.AxisProgressId,
                TrackTitle = head.TrackTitle,
                AxisTitle = head.AxisTitle,
                ExamNumber = head.ExamNumber,
                StartedAtUtc = head.StartedAtUtc,
                SubmittedAtUtc = head.SubmittedAtUtc,
                TimedOut = head.Status == RemedialTrackAttemptStatus.Expired,
                ScorePercent = head.ScorePercent,
                IsPassed = head.IsPassed,
                CanReviewAnswers = true,
                PassPercent = head.PassPercent,
                TotalQuestions = total,
                CorrectCount = correct,
                SkippedCount = skipped,
                WrongCount = total - correct - skipped,
                AllowedMinutes = head.AllowedMinutes,
                SolveMinutes = head.SubmittedAtUtc.HasValue
                    ? Math.Round(Math.Max(0, (head.SubmittedAtUtc.Value - head.StartedAtUtc).TotalMinutes), 1)
                    : 0,
                Tone = ResultToneHelper.Build(head.ScorePercent, ResultContext.Exam, head.PassPercent),
                Sections = sections,
                Lessons = lessons,
                Missed = missed
            });
        }
    }
}
