using QdratNew.Enums;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Services.OnlineLecture
{
    // ═════════════════ مدخلات الدوال النقية (تُقرأ من قاعدة البيانات بعدد استعلامات ثابت) ═════════════════

    public sealed record ReportStudent(int StudentId, string FullName);

    public sealed record ReportPart(int Id, string? Title, string? LessonTitle, int? StartSeconds, int? EndSeconds, int? DurationSeconds);

    public sealed record ReportFullProgress(int StudentId, double WatchedSeconds, double MaxPositionSeconds, int? DurationSeconds,
        bool IsCompleted, DateTime? FirstPingAtUtc, DateTime? LastPingAtUtc, DateTime? CompletedAtUtc);

    public sealed record ReportItemProgress(int ItemId, int StudentId, double WatchedSeconds, bool EndedSeen,
        bool IsCompleted, DateTime? FirstPingAtUtc, DateTime? LastPingAtUtc, DateTime? CompletedAtUtc);

    public sealed record KpiContent(int ContentId, int CurriculumId, string? CurriculumTitle, LectureContentMode Mode,
        int ItemsCount, DateTime LectureStartUtc, DateTime? PublishedAtUtc);

    public sealed record KpiItemCompletion(int ContentId, int StudentId, int CompletedItems);

    /// <summary>
    /// OLC-S7.3 — تقرير المشاهدة لمحاضرة: دالة نقية فوق صفوف التقدّم الحقيقية للطلاب المسجّلين النشطين فقط.
    /// الحالة: أتمّ ← متوقف عند القفل (R13) ← بدأ ← لم يبدأ. «بدأ» = له صف تقدّم (يُنشأ مع أول نبضة).
    /// </summary>
    public static class LectureContentReportBuilder
    {
        public static void Fill(
            LectureContentReportVm vm,
            IReadOnlyList<ReportStudent> enrolled,
            IReadOnlySet<int> lockedStudentIds,
            IReadOnlyList<ReportPart> orderedParts,
            IReadOnlyList<ReportFullProgress> fullProgress,
            IReadOnlyList<ReportItemProgress> itemProgress)
        {
            var isFull = vm.Mode == LectureContentMode.FullVideo;
            var parts = orderedParts.Select((p, i) => new LectureContentReportItemVm
            {
                Id = p.Id,
                Position = i + 1,
                Title = string.IsNullOrWhiteSpace(p.Title) ? p.LessonTitle ?? string.Empty : p.Title!,
                LessonTitle = p.LessonTitle ?? string.Empty,
                StartSeconds = p.StartSeconds,
                EndSeconds = p.EndSeconds,
                DurationSeconds = p.DurationSeconds
            }).ToList();

            var fullByStudent = fullProgress.GroupBy(p => p.StudentId).ToDictionary(g => g.Key, g => g.First());
            var itemsByStudent = itemProgress.GroupBy(p => p.StudentId)
                .ToDictionary(g => g.Key, g => g.GroupBy(x => x.ItemId).ToDictionary(x => x.Key, x => x.First()));

            var students = new List<LectureContentReportStudentVm>(enrolled.Count);
            foreach (var s in enrolled)
            {
                var row = new LectureContentReportStudentVm { StudentId = s.StudentId, FullName = s.FullName };
                bool started, completed;
                LectureContentReportItemVm? stoppedAt = null;

                if (isFull)
                {
                    fullByStudent.TryGetValue(s.StudentId, out var p);
                    started = p is not null;
                    completed = p?.IsCompleted ?? false;
                    var maxPos = p?.MaxPositionSeconds ?? 0;

                    foreach (var part in parts)
                    {
                        var into = started && (completed || maxPos > (part.StartSeconds ?? 0));
                        var reached = completed || (started && LectureWatchRules.ReachedEnd(part.EndSeconds, maxPos));
                        if (into) part.StartedCount++;
                        if (reached) { part.ReachedEndCount++; row.CompletedParts++; }
                        else if (started && stoppedAt is null) stoppedAt = part;
                    }

                    row.WatchedSeconds = (int)Math.Floor(p?.WatchedSeconds ?? 0);
                    row.ProgressPercent = started
                        ? LectureSequenceService.FullPercent(completed, p!.WatchedSeconds, vm.DurationSeconds ?? p.DurationSeconds)
                        : 0;
                    row.FirstActivityUtc = p?.FirstPingAtUtc;
                    row.LastActivityUtc = p?.LastPingAtUtc;
                    row.CompletedAtUtc = completed ? p!.CompletedAtUtc : null;
                }
                else
                {
                    itemsByStudent.TryGetValue(s.StudentId, out var mine);
                    mine ??= new Dictionary<int, ReportItemProgress>();
                    started = mine.Count > 0;
                    var done = 0;

                    foreach (var part in parts)
                    {
                        mine.TryGetValue(part.Id, out var ip);
                        if (ip is not null) part.StartedCount++;
                        if (ip is { EndedSeen: true }) part.ReachedEndCount++;
                        if (ip is { IsCompleted: true }) { part.CompletedCount++; done++; }
                        else if (started && stoppedAt is null) stoppedAt = part;
                    }

                    // R13: «أتمّ» = كل العناصر النشطة مكتملة (العناصر المحذوفة لا تُمرَّر هنا)
                    completed = parts.Count > 0 && done >= parts.Count;
                    row.CompletedParts = done;
                    row.ProgressPercent = parts.Count == 0 ? 0 : (int)Math.Floor(done * 100.0 / parts.Count);
                    var active = mine.Values.Where(v => parts.Any(p => p.Id == v.ItemId)).ToList();
                    row.WatchedSeconds = (int)Math.Floor(active.Sum(v => v.WatchedSeconds));
                    row.FirstActivityUtc = active.Select(v => v.FirstPingAtUtc).Where(d => d.HasValue).DefaultIfEmpty().Min();
                    row.LastActivityUtc = active.Select(v => v.LastPingAtUtc).Where(d => d.HasValue).DefaultIfEmpty().Max();
                    row.CompletedAtUtc = completed ? active.Select(v => v.CompletedAtUtc).Where(d => d.HasValue).DefaultIfEmpty().Max() : null;
                }

                row.State = completed ? LectureReportStudentState.Completed
                    : lockedStudentIds.Contains(s.StudentId) ? LectureReportStudentState.Locked
                    : started ? LectureReportStudentState.InProgress
                    : LectureReportStudentState.NotStarted;

                if (!completed && stoppedAt is not null)
                {
                    stoppedAt.StoppedHereCount++;
                    row.StoppedAt = stoppedAt.Title;
                }

                if (started) vm.StartedCount++;
                students.Add(row);
            }

            vm.Items = parts;
            vm.Students = students
                .OrderBy(x => StateOrder(x.State))
                .ThenBy(x => x.FullName, StringComparer.Ordinal)
                .ToList();
            vm.EnrolledCount = enrolled.Count;
            vm.CompletedCount = students.Count(x => x.State == LectureReportStudentState.Completed);
            vm.LockedCount = students.Count(x => x.State == LectureReportStudentState.Locked);
            vm.InProgressCount = students.Count(x => x.State == LectureReportStudentState.InProgress);
            vm.NotStartedCount = students.Count(x => x.State == LectureReportStudentState.NotStarted);
        }

        // من يحتاج متابعة أولًا: بدأ ولم يُتم، ثم المتوقف عند القفل، ثم من لم يبدأ، ثم من أتم
        private static int StateOrder(LectureReportStudentState s) => s switch
        {
            LectureReportStudentState.InProgress => 0,
            LectureReportStudentState.Locked => 1,
            LectureReportStudentState.NotStarted => 2,
            _ => 3
        };

        /// <summary>
        /// R13 لكل طالب على تسلسل منهج المحاضرة: من لم يُتمّ كل المحاضرات المنشورة السابقة ⇒ مقفلة عليه.
        /// نفس <see cref="LectureSequenceCalculator"/> حرفيًا (المصدر الوحيد لقاعدة القفل).
        /// </summary>
        public static HashSet<int> LockedStudents(
            int targetContentId,
            IReadOnlyList<SequenceLecture> curriculumLecturesTemplate,
            IEnumerable<int> studentIds,
            Func<int, int, bool> isCompleted)
        {
            var locked = new HashSet<int>();
            foreach (var studentId in studentIds)
            {
                var states = LectureSequenceCalculator.Compute(curriculumLecturesTemplate
                    .Select(l => l with { IsCompleted = isCompleted(l.LectureContentId, studentId) }));
                if (states.TryGetValue(targetContentId, out var st) && st.IsLocked)
                    locked.Add(studentId);
            }
            return locked;
        }
    }

    /// <summary>
    /// OLC-S7.4 — KPIs الدفعة الأونلاين (دالة نقية): نسبة المحاضرات المنشورة، متوسط زمن النشر بعد موعد المحاضرة
    /// (من <c>PublishedAt</c> = أول نشر — قرار S3.3)، ونسبة الإتمام لكل منهج للطلاب المسجّلين النشطين.
    /// </summary>
    public static class OnlineBatchKpiCalculator
    {
        public static OnlineBatchKpisVm Compute(
            int lecturesCount,
            IReadOnlyList<KpiContent> published,
            IReadOnlyCollection<int> enrolledStudentIds,
            IEnumerable<(int ContentId, int StudentId)> fullCompletions,
            IEnumerable<KpiItemCompletion> itemCompletions)
        {
            var vm = new OnlineBatchKpisVm
            {
                LecturesCount = lecturesCount,
                PublishedCount = published.Count,
                PublishedPercent = LectureContentReportVm.Percent(published.Count, lecturesCount),
                EnrolledCount = enrolledStudentIds.Count
            };

            var delays = published.Where(c => c.PublishedAtUtc.HasValue)
                .Select(c => (c.PublishedAtUtc!.Value - c.LectureStartUtc).TotalHours)
                .ToList();
            vm.PublishDelaySamples = delays.Count;
            vm.AveragePublishDelayHours = delays.Count == 0 ? null : Math.Round(delays.Average(), 1);

            // إتمام (محتوى، طالب) للمسجّلين فقط: FullVideo = IsCompleted، IndicatorVideos = كل العناصر النشطة مكتملة (R13)
            var enrolled = enrolledStudentIds as IReadOnlySet<int> ?? enrolledStudentIds.ToHashSet();
            var byContent = published.ToDictionary(c => c.ContentId);
            var done = new HashSet<(int ContentId, int StudentId)>();
            foreach (var f in fullCompletions)
            {
                if (enrolled.Contains(f.StudentId) && byContent.TryGetValue(f.ContentId, out var c) && c.Mode == LectureContentMode.FullVideo)
                    done.Add(f);
            }
            foreach (var i in itemCompletions)
            {
                if (enrolled.Contains(i.StudentId) && byContent.TryGetValue(i.ContentId, out var c)
                    && c.Mode == LectureContentMode.IndicatorVideos && c.ItemsCount > 0 && i.CompletedItems >= c.ItemsCount)
                    done.Add((i.ContentId, i.StudentId));
            }

            vm.Curricula = published
                .GroupBy(c => c.CurriculumId)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var ids = g.Select(c => c.ContentId).ToList();
                    var completions = done.Count(d => ids.Contains(d.ContentId));
                    var all = enrolled.Count(s => ids.All(id => done.Contains((id, s))));
                    return new OnlineBatchCurriculumKpiVm
                    {
                        CurriculumId = g.Key,
                        Title = g.Select(c => c.CurriculumTitle).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "غير محدد",
                        PublishedLectures = ids.Count,
                        CompletionPercent = LectureContentReportVm.Percent(completions, enrolled.Count * ids.Count),
                        StudentsCompletedAll = all
                    };
                })
                .ToList();

            return vm;
        }
    }
}
