using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>محاضرة منشورة في تسلسل الطالب مع تقدّمه وحالة القفل المحسوبة على الخادم (R13/R15).</summary>
    public sealed record StudentSequenceLecture(
        int LectureContentId,
        int LectureId,
        string LectureTitle,
        DateTime LectureDate,
        TimeSpan? ScheduledTime,
        int CurriculumId,
        string? CurriculumTitle,
        string? SectionTitle,
        string Title,
        LectureContentMode Mode,
        int ItemsCount,
        int CompletedItems,
        int ProgressPercent,
        bool IsCompleted,
        bool IsLocked)
    {
        /// <summary>المكتملة تُراجع دائمًا؛ غير المكتملة تُفتح فقط إن لم تكن مقفلة.</summary>
        public bool CanOpen => !IsLocked || IsCompleted;
    }

    /// <summary>
    /// OLC-S5.2 — يجمع بيانات القفل التسلسلي لطالب في دفعة ثم يستدعي <see cref="LectureSequenceCalculator"/>.
    /// عدد استعلامات ثابت (المحتويات، تقدّم FullVideo، تقدّم IndicatorVideos مجمّعًا) — لا استعلام لكل محاضرة.
    /// لا يفحص الوصول: المستدعي يمرّر دفعة من <see cref="IOnlineLearningAccessService"/> فقط.
    /// </summary>
    public interface ILectureSequenceService
    {
        /// <summary>محاضرات الدفعة ذات المحتوى المنشور مرتبة بالمنهج ثم بترتيب R13.</summary>
        Task<IReadOnlyList<StudentSequenceLecture>> GetForStudentAsync(int studentId, int batchId, CancellationToken ct = default);
    }

    public sealed class LectureSequenceService : ILectureSequenceService
    {
        private readonly ApplicationDbContext _db;

        public LectureSequenceService(ApplicationDbContext db)
        {
            _db = db;
        }

        private sealed record ContentRow(int Id, int LectureId, string? LectureTitle, DateTime Date, TimeSpan? ScheduledTime,
            int CurriculumId, string? CurriculumTitle, string? SectionTitle, string Title, LectureContentMode Mode,
            int? DurationSeconds, int ItemsCount);

        private sealed record FullRow(int LectureContentId, bool IsCompleted, double WatchedSeconds, int? DurationSeconds);

        private sealed record ItemRow(int ContentId, int Completed);

        public async Task<IReadOnlyList<StudentSequenceLecture>> GetForStudentAsync(int studentId, int batchId, CancellationToken ct = default)
        {
            if (studentId <= 0 || batchId <= 0)
                return Array.Empty<StudentSequenceLecture>();

            // 1) المحتويات المنشورة للدفعة (QueryFilter يستبعد المحذوف، وItems.Count() يعدّ العناصر النشطة فقط)
            var contents = await (
                    from c in _db.LectureContents.AsNoTracking()
                    join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                    where l.BatchId == batchId && c.Status == LectureContentStatus.Published
                    select new ContentRow(
                        c.Id, l.Id, l.Title, l.Date, l.ScheduledTime,
                        l.Section.CurriculumId,
                        l.Section.Curriculum != null ? l.Section.Curriculum.Title : null,
                        l.Section.Title,
                        c.Title, c.ContentMode, c.DurationSeconds,
                        c.Items.Count()))
                .ToListAsync(ct);

            if (contents.Count == 0)
                return Array.Empty<StudentSequenceLecture>();

            // 2) تقدّم FullVideo للطالب في هذه الدفعة
            var full = contents.Any(c => c.Mode == LectureContentMode.FullVideo)
                ? await (
                        from p in _db.LectureContentProgress.AsNoTracking()
                        join c in _db.LectureContents.AsNoTracking() on p.LectureContentId equals c.Id
                        join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                        where p.StudentId == studentId && l.BatchId == batchId
                           && c.Status == LectureContentStatus.Published
                           && c.ContentMode == LectureContentMode.FullVideo
                        select new FullRow(p.LectureContentId, p.IsCompleted, p.WatchedSeconds, p.DurationSeconds))
                    .ToListAsync(ct)
                : new List<FullRow>();

            // 3) IndicatorVideos: عدد العناصر النشطة المكتملة لكل محتوى (تجميع واحد؛ العنصر المحذوف مستبعد بالـ QueryFilter)
            var items = contents.Any(c => c.Mode == LectureContentMode.IndicatorVideos)
                ? await (
                        from ip in _db.LectureContentItemProgress.AsNoTracking()
                        join i in _db.LectureContentItems.AsNoTracking() on ip.LectureContentItemId equals i.Id
                        join c in _db.LectureContents.AsNoTracking() on i.LectureContentId equals c.Id
                        join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                        where ip.StudentId == studentId && ip.IsCompleted && l.BatchId == batchId
                           && c.Status == LectureContentStatus.Published
                           && c.ContentMode == LectureContentMode.IndicatorVideos
                        group ip by i.LectureContentId into g
                        select new ItemRow(g.Key, g.Count()))
                    .ToListAsync(ct)
                : new List<ItemRow>();

            var fullByContent = full.GroupBy(p => p.LectureContentId).ToDictionary(g => g.Key, g => g.First());
            var itemsByContent = items.ToDictionary(x => x.ContentId, x => x.Completed);

            var progress = contents.ToDictionary(c => c.Id, (bool Completed, int CompletedItems, int Percent) (ContentRow c) =>
            {
                if (c.Mode == LectureContentMode.FullVideo)
                {
                    if (!fullByContent.TryGetValue(c.Id, out var p))
                        return (false, 0, 0);

                    return (p.IsCompleted, 0, FullPercent(p.IsCompleted, p.WatchedSeconds, p.DurationSeconds ?? c.DurationSeconds));
                }

                // R13: «أتمّ» = كل العناصر النشطة مكتملة؛ محتوى بلا عناصر لا يُعدّ مكتملًا (R5 يمنع نشره أصلًا)
                var done = Math.Min(itemsByContent.GetValueOrDefault(c.Id), c.ItemsCount);
                var completed = c.ItemsCount > 0 && done >= c.ItemsCount;
                var percent = c.ItemsCount == 0 ? 0 : (int)Math.Floor(done * 100.0 / c.ItemsCount);
                return (completed, done, percent);
            });

            var states = LectureSequenceCalculator.Compute(contents.Select(c =>
                new SequenceLecture(c.Id, c.LectureId, c.CurriculumId, c.Date, c.ScheduledTime, progress[c.Id].Completed)));

            return contents
                .OrderBy(c => c.CurriculumId)
                .ThenBy(c => c.Date.Date)
                .ThenBy(c => c.ScheduledTime ?? TimeSpan.Zero)
                .ThenBy(c => c.LectureId)
                .Select(c =>
                {
                    var p = progress[c.Id];
                    var s = states[c.Id];
                    return new StudentSequenceLecture(
                        c.Id, c.LectureId, c.LectureTitle ?? string.Empty, c.Date, c.ScheduledTime,
                        c.CurriculumId, c.CurriculumTitle, c.SectionTitle, c.Title, c.Mode,
                        c.ItemsCount, p.CompletedItems, p.Percent, s.IsCompleted, s.IsLocked);
                })
                .ToList();
        }

        // رصيد المشاهدة بزمن الخادم مقابل المدة؛ غير المكتمل لا يُعرض 100%
        public static int FullPercent(bool isCompleted, double watchedSeconds, int? durationSeconds)
        {
            if (isCompleted)
                return 100;

            if (durationSeconds is not > 0)
                return 0;

            return Math.Clamp((int)Math.Floor(watchedSeconds * 100.0 / durationSeconds.Value), 0, 99);
        }
    }
}
