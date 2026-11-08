using Microsoft.EntityFrameworkCore;
using QdratNew.Enums;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>
    /// OLC-S7.3/S7.4 — متابعة الأدمن من بيانات التقدّم الحقيقية فقط. عدد استعلامات ثابت لكل شاشة (لا استعلام لكل طالب أو محاضرة)،
    /// ولا <c>Contains</c> على قائمة في الذاكرة داخل EF (SQL Server 2014): الطلاب المسجّلون يُربطون بـ Join/EXISTS على
    /// <c>StudentBatchEnrollments</c>، والتجميع النهائي في الدوال النقية <see cref="LectureContentReportBuilder"/> و<see cref="OnlineBatchKpiCalculator"/>.
    /// «المسجّلون» = تسجيل نشط (<c>Status = "Active"</c>) لطالب <c>IsActiveForLearning</c> — نفس تعريف وصول الطالب (R2).
    /// </summary>
    public sealed partial class LectureContentAdminService
    {
        private sealed record CurriculumContentRow(int ContentId, int LectureId, DateTime Date, TimeSpan? ScheduledTime,
            LectureContentMode Mode, int ItemsCount);

        // ═══════════════════════════ OLC-S7.3 — تقرير المحاضرة ═══════════════════════════

        public async Task<LectureContentReportVm?> GetReportAsync(int lectureId, CancellationToken ct = default)
        {
            var head = await (
                    from c in _db.LectureContents.AsNoTracking()
                    join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                    where c.LectureId == lectureId
                    select new
                    {
                        ContentId = c.Id, c.Title, c.ContentMode, c.Status, c.PublishedAt, c.DurationSeconds,
                        LectureTitle = l.Title, l.Date, l.BatchId,
                        BatchName = l.Batch.Name,
                        SectionTitle = l.Section.Title,
                        l.Section.CurriculumId,
                        CurriculumTitle = l.Section.Curriculum != null ? l.Section.Curriculum.Title : null
                    })
                .FirstOrDefaultAsync(ct);
            if (head is null)
                return null;

            var vm = new LectureContentReportVm
            {
                LectureId = lectureId,
                ContentId = head.ContentId,
                BatchId = head.BatchId,
                BatchName = head.BatchName ?? string.Empty,
                LectureTitle = head.LectureTitle ?? string.Empty,
                LectureDate = head.Date,
                SectionTitle = head.SectionTitle ?? string.Empty,
                CurriculumTitle = head.CurriculumTitle ?? string.Empty,
                ContentTitle = head.Title,
                Mode = head.ContentMode,
                Status = head.Status,
                PublishedAt = head.PublishedAt,
                DurationSeconds = head.DurationSeconds
            };

            // 1) الطلاب المسجّلون النشطون في دفعة المحاضرة
            var enrolled = (await _db.StudentBatchEnrollments.AsNoTracking()
                    .Where(e => e.BatchId == head.BatchId && e.Status == ActiveEnrollmentStatus && e.Student.IsActiveForLearning)
                    .Select(e => new { e.StudentID, e.Student.FullName })
                    .Distinct()
                    .ToListAsync(ct))
                .GroupBy(x => x.StudentID)
                .Select(g => new ReportStudent(g.Key, g.First().FullName ?? string.Empty))
                .ToList();

            // 2) الفصول/الفيديوهات النشطة (عنوان المؤشر بـ LEFT JOIN حتى لا يسقط عنصر)
            var parts = await (
                    from i in _db.LectureContentItems.AsNoTracking()
                    join les in _db.Lessons.AsNoTracking() on i.LessonId equals les.Id into lj
                    from les in lj.DefaultIfEmpty()
                    where i.LectureContentId == head.ContentId
                    select new { i.Id, i.Title, LessonTitle = les != null ? les.Title : null, i.SortOrder, i.StartSeconds, i.EndSeconds, i.DurationSeconds })
                .ToListAsync(ct);
            var orderedParts = (head.ContentMode == LectureContentMode.FullVideo
                    ? parts.OrderBy(p => p.StartSeconds ?? int.MaxValue).ThenBy(p => p.SortOrder).ThenBy(p => p.Id)
                    : parts.OrderBy(p => p.SortOrder).ThenBy(p => p.Id))
                .Select(p => new ReportPart(p.Id, p.Title, p.LessonTitle, p.StartSeconds, p.EndSeconds, p.DurationSeconds))
                .ToList();

            // 3) تقدّم هذا المحتوى (كل الصفوف — المسجّلون فقط يُحتسبون في الدالة النقية)
            var full = new List<ReportFullProgress>();
            var items = new List<ReportItemProgress>();
            if (head.ContentMode == LectureContentMode.FullVideo)
            {
                full = await _db.LectureContentProgress.AsNoTracking()
                    .Where(p => p.LectureContentId == head.ContentId)
                    .Select(p => new ReportFullProgress(p.StudentId, p.WatchedSeconds, p.MaxPositionSeconds, p.DurationSeconds,
                        p.IsCompleted, p.FirstPingAtUtc, p.LastPingAtUtc, p.CompletedAtUtc))
                    .ToListAsync(ct);
            }
            else
            {
                items = await (
                        from p in _db.LectureContentItemProgress.AsNoTracking()
                        join i in _db.LectureContentItems.AsNoTracking() on p.LectureContentItemId equals i.Id
                        where i.LectureContentId == head.ContentId
                        select new ReportItemProgress(p.LectureContentItemId, p.StudentId, p.WatchedSeconds, p.EndedSeen,
                            p.IsCompleted, p.FirstPingAtUtc, p.LastPingAtUtc, p.CompletedAtUtc))
                    .ToListAsync(ct);
            }

            // 4) R13: من يقف عند القفل قبل هذه المحاضرة — فقط للمنشور (المخفي/المسودة خارج التسلسل)
            var locked = new HashSet<int>();
            if (head.Status == LectureContentStatus.Published)
            {
                var curriculumContents = await (
                        from c in _db.LectureContents.AsNoTracking()
                        join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                        where l.BatchId == head.BatchId && l.Section.CurriculumId == head.CurriculumId
                           && c.Status == LectureContentStatus.Published
                        select new CurriculumContentRow(c.Id, l.Id, l.Date, l.ScheduledTime, c.ContentMode, c.Items.Count()))
                    .ToListAsync(ct);

                var ordered = curriculumContents
                    .OrderBy(x => x.Date.Date).ThenBy(x => x.ScheduledTime ?? TimeSpan.Zero).ThenBy(x => x.LectureId)
                    .ToList();
                vm.SequenceCount = ordered.Count;
                vm.SequencePosition = ordered.FindIndex(x => x.ContentId == head.ContentId) + 1;

                if (vm.SequencePosition > 1 && enrolled.Count > 0)
                {
                    var completions = await CurriculumCompletionsAsync(head.BatchId, head.CurriculumId, curriculumContents, ct);
                    var template = ordered
                        .Select(x => new SequenceLecture(x.ContentId, x.LectureId, head.CurriculumId, x.Date, x.ScheduledTime, false))
                        .ToList();
                    locked = LectureContentReportBuilder.LockedStudents(head.ContentId, template, enrolled.Select(e => e.StudentId),
                        (contentId, studentId) => completions.Contains((contentId, studentId)));
                }
            }

            LectureContentReportBuilder.Fill(vm, enrolled, locked, orderedParts, full, items);
            return vm;
        }

        /// <summary>
        /// إتمام (محتوى، طالب) لمحتويات منهج واحد منشورة في الدفعة — استعلامان مجمّعان:
        /// FullVideo = IsCompleted، IndicatorVideos = عدد العناصر النشطة المكتملة = عدد العناصر (R13).
        /// </summary>
        private async Task<HashSet<(int ContentId, int StudentId)>> CurriculumCompletionsAsync(
            int batchId, int curriculumId, IReadOnlyList<CurriculumContentRow> contents, CancellationToken ct)
        {
            var done = new HashSet<(int, int)>();

            if (contents.Any(c => c.Mode == LectureContentMode.FullVideo))
            {
                var full = await (
                        from p in _db.LectureContentProgress.AsNoTracking()
                        join c in _db.LectureContents.AsNoTracking() on p.LectureContentId equals c.Id
                        join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                        where p.IsCompleted && l.BatchId == batchId && l.Section.CurriculumId == curriculumId
                           && c.Status == LectureContentStatus.Published && c.ContentMode == LectureContentMode.FullVideo
                        select new { p.LectureContentId, p.StudentId })
                    .ToListAsync(ct);
                foreach (var f in full)
                    done.Add((f.LectureContentId, f.StudentId));
            }

            if (contents.Any(c => c.Mode == LectureContentMode.IndicatorVideos))
            {
                var itemsCount = contents.ToDictionary(c => c.ContentId, c => c.ItemsCount);
                var grouped = await (
                        from ip in _db.LectureContentItemProgress.AsNoTracking()
                        join i in _db.LectureContentItems.AsNoTracking() on ip.LectureContentItemId equals i.Id
                        join c in _db.LectureContents.AsNoTracking() on i.LectureContentId equals c.Id
                        join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                        where ip.IsCompleted && l.BatchId == batchId && l.Section.CurriculumId == curriculumId
                           && c.Status == LectureContentStatus.Published && c.ContentMode == LectureContentMode.IndicatorVideos
                        group ip by new { i.LectureContentId, ip.StudentId } into g
                        select new { g.Key.LectureContentId, g.Key.StudentId, Count = g.Count() })
                    .ToListAsync(ct);
                foreach (var g in grouped)
                {
                    if (itemsCount.TryGetValue(g.LectureContentId, out var total) && total > 0 && g.Count >= total)
                        done.Add((g.LectureContentId, g.StudentId));
                }
            }

            return done;
        }

        // ═══════════════════════════ OLC-S7.4 — KPIs الدفعة ═══════════════════════════

        public async Task<OnlineBatchKpisVm> GetBatchKpisAsync(int batchId, CancellationToken ct = default)
        {
            var lecturesCount = await _db.Lecture.AsNoTracking().CountAsync(l => l.BatchId == batchId, ct);
            if (lecturesCount == 0)
                return new OnlineBatchKpisVm();

            var published = await (
                    from c in _db.LectureContents.AsNoTracking()
                    join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                    where l.BatchId == batchId && c.Status == LectureContentStatus.Published
                    select new
                    {
                        c.Id, l.Section.CurriculumId,
                        CurriculumTitle = l.Section.Curriculum != null ? l.Section.Curriculum.Title : null,
                        c.ContentMode, ItemsCount = c.Items.Count(), l.Date, l.ScheduledTime, c.PublishedAt
                    })
                .ToListAsync(ct);

            var contents = published
                .Select(p => new KpiContent(p.Id, p.CurriculumId, p.CurriculumTitle, p.ContentMode, p.ItemsCount,
                    LectureStartUtc(p.Date, p.ScheduledTime), p.PublishedAt))
                .ToList();

            var enrolled = await _db.StudentBatchEnrollments.AsNoTracking()
                .Where(e => e.BatchId == batchId && e.Status == ActiveEnrollmentStatus && e.Student.IsActiveForLearning)
                .Select(e => e.StudentID)
                .Distinct()
                .ToListAsync(ct);

            var fullCompletions = new List<(int, int)>();
            var itemCompletions = new List<KpiItemCompletion>();
            if (contents.Any(c => c.Mode == LectureContentMode.FullVideo))
            {
                var rows = await (
                        from p in _db.LectureContentProgress.AsNoTracking()
                        join c in _db.LectureContents.AsNoTracking() on p.LectureContentId equals c.Id
                        join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                        where p.IsCompleted && l.BatchId == batchId
                           && c.Status == LectureContentStatus.Published && c.ContentMode == LectureContentMode.FullVideo
                        select new { p.LectureContentId, p.StudentId })
                    .ToListAsync(ct);
                fullCompletions = rows.Select(r => (r.LectureContentId, r.StudentId)).ToList();
            }
            if (contents.Any(c => c.Mode == LectureContentMode.IndicatorVideos))
            {
                itemCompletions = await (
                        from ip in _db.LectureContentItemProgress.AsNoTracking()
                        join i in _db.LectureContentItems.AsNoTracking() on ip.LectureContentItemId equals i.Id
                        join c in _db.LectureContents.AsNoTracking() on i.LectureContentId equals c.Id
                        join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                        where ip.IsCompleted && l.BatchId == batchId
                           && c.Status == LectureContentStatus.Published && c.ContentMode == LectureContentMode.IndicatorVideos
                        group ip by new { i.LectureContentId, ip.StudentId } into g
                        select new KpiItemCompletion(g.Key.LectureContentId, g.Key.StudentId, g.Count()))
                    .ToListAsync(ct);
            }

            return OnlineBatchKpiCalculator.Compute(lecturesCount, contents, enrolled.ToHashSet(), fullCompletions, itemCompletions);
        }

        // Lecture.Date + ScheduledTime بتوقيت السعودية (كما يُدخلها الأدمن) ← UTC للمقارنة مع PublishedAt (UTC)
        private DateTime LectureStartUtc(DateTime date, TimeSpan? scheduledTime)
            => _tz.ConvertToUtc(DateTime.SpecifyKind(date.Date + (scheduledTime ?? TimeSpan.Zero), DateTimeKind.Unspecified));
    }
}
