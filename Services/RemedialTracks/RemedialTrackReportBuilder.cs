using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S6/S11.1: البناء المشترك لتقرير تسجيل واحد — 5 استعلامات ثابتة (ترويسة، محاور، تقدّم فيديو مجمّع، محاولات، ملاحق — S13).
    /// يُستخدم من خدمة التقارير ومن إنشاء لقطة ولي الأمر داخل معاملة التسليم (نفس الأرقام، بلا حلقات استعلام).
    /// </summary>
    public static class RemedialTrackReportBuilder
    {
        public static async Task<RemedialTrackEnrollmentReportVm?> BuildAsync(
            ApplicationDbContext db, int enrollmentId, int? ownerStudentId, RemedialTrackBatchScope? scope,
            bool includeNote, DateTime nowUtc, CancellationToken ct)
        {
            var headQuery = db.RemedialTrackEnrollments.AsNoTracking().Where(e => e.Id == enrollmentId);
            if (ownerStudentId.HasValue)
            {
                var owner = ownerStudentId.Value;
                headQuery = headQuery.Where(e => e.StudentId == owner);
            }

            var head = await headQuery
                .Where(e => !e.Publication!.IsDeleted)   // RTK v2/D26: أمر محذوف ← لا تقرير (طالب/ولي أمر/أدمن)
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
                    VideoTotal = db.RemedialTrackVideos.Count(v => v.AxisId == a.AxisId && v.IsActive),
                    VideoSeconds = db.RemedialTrackVideos.Where(v => v.AxisId == a.AxisId && v.IsActive).Sum(v => v.DurationSeconds ?? 0)
                })
                .ToListAsync(ct);

            // تقدّم الفيديو مجمّعًا لكل (محور، جولة): المكتمل + ثواني المشاهدة + مدة الفيديوهات المسجّلة (للتقييد)
            var videoAgg = await db.RemedialTrackVideoProgresses.AsNoTracking()
                .Where(v => v.AxisProgress!.EnrollmentId == enrollmentId)
                .GroupBy(v => new { v.AxisProgressId, v.Round })
                .Select(g => new
                {
                    g.Key.AxisProgressId,
                    g.Key.Round,
                    Done = g.Count(x => x.IsCompleted),
                    Watched = g.Sum(x => x.WatchedSeconds),
                    Duration = g.Sum(x => x.DurationSeconds ?? 0)
                })
                .ToListAsync(ct);

            var attempts = await db.RemedialTrackExamAttempts.AsNoTracking()
                .Where(t => t.AxisProgress!.EnrollmentId == enrollmentId && t.Status != RemedialTrackAttemptStatus.InProgress
                            && t.AddendumId == null)   // RTK-S13/D33: محاولات الملحق خارج نسب المحاور
                .OrderBy(t => t.AxisProgressId).ThenBy(t => t.StartedAtUtc).ThenBy(t => t.ExamNumber)
                .Select(t => new
                {
                    t.AxisProgressId,
                    t.ExamNumber,
                    SubmittedAtUtc = t.SubmittedAtUtc ?? t.ExpiresAtUtc,
                    t.StartedAtUtc,
                    t.CorrectCount,
                    t.TotalQuestions,
                    t.ScorePercent,
                    t.IsPassed
                })
                .ToListAsync(ct);

            // RTK-S13/D33: الملاحق الفعّالة لهذا التسجيل — استعلام واحد، سطر مستقل «مطلوب إضافي» لا يدخل في نسب المحاور
            var addenda = await db.RemedialTrackAddendumProgresses.AsNoTracking()
                .Where(p => p.EnrollmentId == enrollmentId && p.Addendum!.IsActive
                            // D31: ملحق محور لم يُفتح بعد للطالب لا يظهر في تقريره (كما لا يظهر في خطته)
                            && p.Enrollment!.AxisProgresses.Any(a => a.AxisId == p.Addendum.AxisId && a.Status != RemedialTrackAxisStatus.Locked))
                .OrderBy(p => p.Addendum!.CreatedAtUtc).ThenBy(p => p.AddendumId)
                .Select(p => new RemedialTrackReportAddendumVm
                {
                    Title = p.Addendum!.Title,
                    AxisTitle = p.Addendum.Axis!.TitleOverride ?? p.Addendum.Axis.Section!.Title,
                    Completed = p.CompletedAtUtc != null
                })
                .ToListAsync(ct);

            var rows = axes.Select(a =>
            {
                var r1 = videoAgg.FirstOrDefault(v => v.AxisProgressId == a.Id && v.Round == 1);
                var r2 = videoAgg.FirstOrDefault(v => v.AxisProgressId == a.Id && v.Round == 2);
                DateTime? SubmittedAt(RemedialTrackExamNumber n) => attempts
                    .Where(t => t.AxisProgressId == a.Id && t.ExamNumber == n)
                    .Select(t => (DateTime?)t.SubmittedAtUtc).FirstOrDefault();

                var round2Done = r2?.Done ?? 0;
                var hasRound2 = a.Round >= 2 || round2Done > 0;
                var path = RemedialTrackRecommendations.PathOf(a.Status, a.Exam101Percent, a.Exam102Percent);

                var min1 = MinutesOf(r1?.Watched ?? 0, a.VideoSeconds, r1?.Duration ?? 0);
                var min2 = MinutesOf(r2?.Watched ?? 0, a.VideoSeconds, r2?.Duration ?? 0);
                var videoMinutes = (int)Math.Round(a.VideoSeconds / 60.0, MidpointRounding.AwayFromZero);
                double? finalPercent = a.Exam102Percent ?? a.Exam101Percent;

                return new RemedialTrackReportAxisVm
                {
                    Order = a.Order,
                    Title = a.Title,
                    Status = a.Status,
                    Outcome = OutcomeOf(a.Status),
                    Round = a.Round,
                    VideoTotal = a.VideoTotal,
                    VideosDoneRound1 = r1?.Done ?? 0,
                    VideosDoneRound2 = round2Done,
                    HasRound2 = hasRound2,
                    Exam101Percent = a.Exam101Percent,
                    Exam101SubmittedAtUtc = SubmittedAt(RemedialTrackExamNumber.Exam101),
                    Exam102Percent = a.Exam102Percent,
                    Exam102SubmittedAtUtc = SubmittedAt(RemedialTrackExamNumber.Exam102),
                    PassedAtUtc = a.PassedAtUtc,
                    AdminOpenedAtUtc = a.AdminOpenedAtUtc,
                    WatchedMinutesRound1 = min1,
                    WatchedMinutesRound2 = min2,
                    VideoMinutes = videoMinutes,
                    PathKind = path,
                    Attempts = attempts
                        .Where(t => t.AxisProgressId == a.Id)
                        .Select(t => new RemedialTrackReportAttemptVm
                        {
                            ExamNumber = t.ExamNumber,
                            SubmittedAtUtc = t.SubmittedAtUtc,
                            CorrectCount = t.CorrectCount,
                            TotalQuestions = t.TotalQuestions,
                            ScorePercent = t.ScorePercent,
                            IsPassed = t.IsPassed,
                            DurationMinutes = (int)Math.Max(0, Math.Round((t.SubmittedAtUtc - t.StartedAtUtc).TotalMinutes, MidpointRounding.AwayFromZero))
                        }).ToList(),
                    Recommendation = RemedialTrackRecommendations.For(
                        path,
                        finalPercent.HasValue ? (int)Math.Round(finalPercent.Value, MidpointRounding.AwayFromZero) : null,
                        head.PassPercent,
                        min1 + min2,
                        videoMinutes * (hasRound2 ? 2 : 1))
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
                IssuedAtUtc = nowUtc,
                AdminNote = includeNote ? head.AdminReportNote : null,
                Axes = rows,
                Addenda = addenda
            };
        }

        /// <summary>
        /// دقائق المشاهدة مقرَّبة لأقرب دقيقة ومقيّدة بمدة فيديوهات المحور للجولة الواحدة (لا قيم تفوق الواقع من نبضات مكررة).
        /// المرجع: مدة الفيديوهات المعتمدة للمحور، وإن كانت غير معروفة فمدة التقدّم المسجّلة، وإن غابت فلا تقييد.
        /// </summary>
        public static int MinutesOf(double watchedSeconds, int axisVideoSeconds, int progressDurationSeconds)
        {
            var cap = axisVideoSeconds > 0 ? axisVideoSeconds : progressDurationSeconds;
            var seconds = Math.Max(0, watchedSeconds);
            if (cap > 0) seconds = Math.Min(seconds, cap);
            return (int)Math.Round(seconds / 60.0, MidpointRounding.AwayFromZero);
        }

        public static RemedialTrackReportAxisOutcome OutcomeOf(RemedialTrackAxisStatus status) => status switch
        {
            RemedialTrackAxisStatus.Passed => RemedialTrackReportAxisOutcome.Passed,
            RemedialTrackAxisStatus.FailedBlocked => RemedialTrackReportAxisOutcome.NeedsFollowUp,
            RemedialTrackAxisStatus.FailedOpenedByAdmin => RemedialTrackReportAxisOutcome.MovedByAdmin,
            RemedialTrackAxisStatus.Locked => RemedialTrackReportAxisOutcome.NotReached,
            _ => RemedialTrackReportAxisOutcome.InProgress
        };
    }
}
