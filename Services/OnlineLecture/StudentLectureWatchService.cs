using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>ثوابت R9 المشتركة بين صفحة المشاهدة (OLC-S6) والنبضة (OLC-S7) — نفس قيم العقد حرفيًا.</summary>
    public static class LectureWatchRules
    {
        public const int MinWatchPercent = 80;
        public const int MaxCreditPerPingSeconds = 20;
        public const int MaxGapCreditSeconds = 600;
        public const int PositionToleranceSeconds = 3;

        /// <summary>R9: ceil(Duration × 80%)؛ مدة غير معروفة ⇒ 0.</summary>
        public static int RequiredSeconds(int? durationSeconds)
            => durationSeconds is > 0 ? (int)Math.Ceiling(durationSeconds.Value * MinWatchPercent / 100.0) : 0;

        /// <summary>R9: «وصلتَ لنهاية الفصل» = MaxPositionSeconds ≥ EndSeconds − 3.</summary>
        public static bool ReachedEnd(int? endSeconds, double maxPositionSeconds)
            => endSeconds.HasValue && maxPositionSeconds >= endSeconds.Value - PositionToleranceSeconds;
    }

    /// <summary>عنصر محتوى كما يُقرأ من قاعدة البيانات مع تقدّم الطالب عليه (مدخل دالة البناء النقية).</summary>
    public sealed record LectureWatchItemSource(
        int Id, string? LessonTitle, string? Title, int SortOrder, int? StartSeconds, int? EndSeconds,
        LectureVideoProvider? Provider, string? ExternalId, int? DurationSeconds,
        bool IsCompleted, double MaxPositionSeconds);

    /// <summary>
    /// OLC-S6 — دوال نقية لبناء عناصر صفحة المشاهدة (R14 + C.7-P4/P6/P11). لا قاعدة بيانات ولا وقت — تُختبر وحدةً وتُنقل لـ OLN.
    /// </summary>
    public static class LectureWatchBuilder
    {
        /// <summary>
        /// IndicatorVideos (R14): العنصر k مفتوح فقط إن اكتمل كل ما قبله. المصدر (P6) يُرسل للمكتمل (مراجعة P11)
        /// وللعنصر المفتوح الحالي وللتالي مباشرة فقط؛ وفي مراجعة المحاضرة المكتملة كل العناصر مفتوحة.
        /// </summary>
        public static List<StudentLectureWatchItemVm> BuildPlaylist(IEnumerable<LectureWatchItemSource> ordered, bool lectureReview)
        {
            var result = new List<StudentLectureWatchItemVm>();
            var previousDone = true;
            var revealedNext = false;

            foreach (var s in ordered)
            {
                var unlocked = lectureReview || previousDone;
                var expose = unlocked;
                if (!unlocked && !revealedNext)
                {
                    expose = true;          // التالي مباشرة: يُحمَّل فور إتمام الحالي دون كشف ما بعده
                    revealedNext = true;
                }

                result.Add(new StudentLectureWatchItemVm
                {
                    Id = s.Id,
                    LessonTitle = s.LessonTitle ?? string.Empty,
                    Title = s.Title ?? string.Empty,
                    SortOrder = s.SortOrder,
                    Embed = expose ? Embed(s.Provider, s.ExternalId) : null,
                    DurationSeconds = s.DurationSeconds,
                    RequiredSeconds = LectureWatchRules.RequiredSeconds(s.DurationSeconds),
                    ResumeAtSeconds = lectureReview || s.IsCompleted ? 0 : (int)Math.Floor(Math.Max(0, s.MaxPositionSeconds)),
                    IsCompleted = s.IsCompleted,
                    IsUnlocked = unlocked,
                    ReachedEnd = s.IsCompleted
                });

                previousDone = previousDone && s.IsCompleted;
            }

            return result;
        }

        /// <summary>
        /// FullVideo: الفصول بلا مصدر (المصدر واحد للمحتوى). القفز لفصل مسموح فقط إن كانت بدايته داخل ما شوهد (P4)؛
        /// «وصلتَ لنهايته» من MaxPositionSeconds على الخادم (R9). المراجعة/المكتمل ⇒ كل الفصول متاحة.
        /// </summary>
        public static List<StudentLectureWatchItemVm> BuildChapters(IEnumerable<LectureWatchItemSource> ordered, double maxPositionSeconds, bool freeNavigation)
        {
            return ordered.Select(s =>
            {
                var reached = freeNavigation || LectureWatchRules.ReachedEnd(s.EndSeconds, maxPositionSeconds);
                return new StudentLectureWatchItemVm
                {
                    Id = s.Id,
                    LessonTitle = s.LessonTitle ?? string.Empty,
                    Title = s.Title ?? string.Empty,
                    SortOrder = s.SortOrder,
                    StartSeconds = s.StartSeconds,
                    EndSeconds = s.EndSeconds,
                    IsUnlocked = freeNavigation || (s.StartSeconds ?? 0) <= maxPositionSeconds,
                    ReachedEnd = reached,
                    IsCompleted = reached
                };
            }).ToList();
        }

        /// <summary>R8: الرابط يُبنى من المعرّف فقط؛ بيانات قديمة غير صالحة ⇒ لا مصدر.</summary>
        public static StudentLectureEmbedVm? Embed(LectureVideoProvider? provider, string? externalId)
        {
            var url = LectureVideoUrlParser.TryBuildEmbedUrl(provider, externalId);
            return url is null ? null : new StudentLectureEmbedVm { Provider = provider!.Value, ExternalId = externalId!, EmbedUrl = url };
        }
    }

    public enum LectureWatchOutcome
    {
        /// <summary>المفتاح مغلق أو R2 غير متحقق — 404 بلا تمييز.</summary>
        NotFound = 0,
        /// <summary>R15: المحاضرة مقفلة — لا تُرجع أي بيانات فيديو.</summary>
        Locked = 1,
        Ok = 2
    }

    public sealed record LectureWatchResult(LectureWatchOutcome Outcome, StudentLectureWatchVm? Model)
    {
        public static readonly LectureWatchResult NotFound = new(LectureWatchOutcome.NotFound, null);
        public static readonly LectureWatchResult Locked = new(LectureWatchOutcome.Locked, null);
    }

    /// <summary>
    /// OLC-S6 — صفحة المشاهدة المحمية: المفتاح ← R2 (<see cref="IOnlineLearningAccessService.CanViewAsync"/>) ← R15
    /// (<see cref="ILectureSequenceService"/>) ثم بناء المشغّل. studentId من سياق الطالب في الخادم فقط.
    /// </summary>
    public interface IStudentLectureWatchService
    {
        Task<LectureWatchResult> GetWatchAsync(int studentId, int lectureContentId, CancellationToken ct = default);
    }

    public sealed class StudentLectureWatchService : IStudentLectureWatchService
    {
        private readonly ApplicationDbContext _db;
        private readonly IOnlineLearningAccessService _access;
        private readonly ILectureSequenceService _sequence;

        public StudentLectureWatchService(ApplicationDbContext db, IOnlineLearningAccessService access, ILectureSequenceService sequence)
        {
            _db = db;
            _access = access;
            _sequence = sequence;
        }

        public async Task<LectureWatchResult> GetWatchAsync(int studentId, int lectureContentId, CancellationToken ct = default)
        {
            // R2 (+ المفتاح): أي شرط ناقص = 404
            var access = await _access.CanViewAsync(studentId, lectureContentId, ct);
            if (access is null)
                return LectureWatchResult.NotFound;

            // R13/R15 على الخادم: نفس حساب القائمة حرفيًا
            var sequence = await _sequence.GetForStudentAsync(studentId, access.BatchId, ct);
            var index = -1;
            for (var i = 0; i < sequence.Count; i++)
            {
                if (sequence[i].LectureContentId == lectureContentId) { index = i; break; }
            }
            if (index < 0)
                return LectureWatchResult.NotFound;

            var row = sequence[index];
            if (!row.CanOpen)
                return LectureWatchResult.Locked;

            var content = await _db.LectureContents.AsNoTracking()
                .Where(c => c.Id == lectureContentId)
                .Select(c => new { c.Title, c.Description, c.ContentMode, c.VideoProvider, c.VideoExternalId, c.DurationSeconds })
                .FirstOrDefaultAsync(ct);
            if (content is null)
                return LectureWatchResult.NotFound;

            // P1: العلامة المائية من سجل الطالب نفسه (استعلام صغير منفصل كما في RTK)
            var who = await _db.Students.AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => new { s.FullName, s.NationalID })
                .FirstOrDefaultAsync(ct);

            // العناصر النشطة (الحذف الناعم بالـ QueryFilter) مرتبة. عنوان المؤشر بـ LEFT JOIN صريح: لا يسقط عنصر من
            // قائمة التشغيل بسبب صف المؤشر، وإلا اختلف عدد العناصر هنا عن عدّ التسلسل (R13) فلا تكتمل المحاضرة أبدًا.
            var items = await (
                    from i in _db.LectureContentItems.AsNoTracking()
                    join l in _db.Lessons.AsNoTracking() on i.LessonId equals l.Id into lj
                    from l in lj.DefaultIfEmpty()
                    where i.LectureContentId == lectureContentId
                    orderby i.SortOrder, i.Id
                    select new
                    {
                        i.Id, i.Title, i.SortOrder, i.StartSeconds, i.EndSeconds, i.VideoProvider, i.VideoExternalId, i.DurationSeconds,
                        LessonTitle = l != null ? l.Title : null
                    })
                .ToListAsync(ct);

            var review = row.IsCompleted;   // P11
            var vm = new StudentLectureWatchVm
            {
                Id = lectureContentId,
                Title = content.Title,
                Description = content.Description,
                Mode = content.ContentMode,
                LectureTitle = row.LectureTitle,
                LectureDate = row.LectureDate,
                SectionTitle = string.IsNullOrWhiteSpace(row.SectionTitle) ? "غير محدد" : row.SectionTitle!,
                CurriculumTitle = row.CurriculumTitle ?? string.Empty,
                IsReview = review,
                IsCompleted = row.IsCompleted,
                ProgressPercent = row.ProgressPercent,
                Watermark = new StudentLectureWatermarkVm
                {
                    Name = who?.FullName ?? string.Empty,
                    NationalId = (who?.NationalID ?? string.Empty).Trim()
                }
            };

            if (content.ContentMode == LectureContentMode.FullVideo)
            {
                var progress = await _db.LectureContentProgress.AsNoTracking()
                    .Where(p => p.LectureContentId == lectureContentId && p.StudentId == studentId)
                    .Select(p => new { p.MaxPositionSeconds, p.DurationSeconds })
                    .FirstOrDefaultAsync(ct);

                var maxPos = Math.Max(0, progress?.MaxPositionSeconds ?? 0);
                vm.Embed = LectureWatchBuilder.Embed(content.VideoProvider, content.VideoExternalId);
                vm.DurationSeconds = content.DurationSeconds ?? progress?.DurationSeconds;
                vm.RequiredSeconds = LectureWatchRules.RequiredSeconds(vm.DurationSeconds);
                vm.ResumeAtSeconds = review ? 0 : (int)Math.Floor(maxPos);
                vm.Items = LectureWatchBuilder.BuildChapters(
                    items.OrderBy(i => i.StartSeconds ?? int.MaxValue).ThenBy(i => i.SortOrder).ThenBy(i => i.Id)
                        .Select(i => new LectureWatchItemSource(i.Id, i.LessonTitle, i.Title, i.SortOrder, i.StartSeconds, i.EndSeconds,
                            null, null, null, false, 0)),
                    maxPos, freeNavigation: review);
            }
            else
            {
                // تقدّم الطالب على عناصر هذا المحتوى فقط (Join لا Contains على قائمة في الذاكرة)
                var progress = await (
                        from p in _db.LectureContentItemProgress.AsNoTracking()
                        join i in _db.LectureContentItems.AsNoTracking() on p.LectureContentItemId equals i.Id
                        where i.LectureContentId == lectureContentId && p.StudentId == studentId
                        select new { p.LectureContentItemId, p.IsCompleted, p.MaxPositionSeconds })
                    .ToListAsync(ct);
                var byItem = progress.GroupBy(p => p.LectureContentItemId).ToDictionary(g => g.Key, g => g.First());

                vm.Items = LectureWatchBuilder.BuildPlaylist(
                    items.Select(i =>
                    {
                        var p = byItem.GetValueOrDefault(i.Id);
                        return new LectureWatchItemSource(i.Id, i.LessonTitle, i.Title, i.SortOrder, null, null,
                            i.VideoProvider, i.VideoExternalId, i.DurationSeconds, p?.IsCompleted ?? false, p?.MaxPositionSeconds ?? 0);
                    }),
                    lectureReview: review);
            }

            // المحاضرة التالية في نفس المنهج (R13) — فتحها يقرره الخادم
            if (index + 1 < sequence.Count && sequence[index + 1].CurriculumId == row.CurriculumId)
            {
                var next = sequence[index + 1];
                vm.NextLectureContentId = next.LectureContentId;
                vm.NextLectureTitle = next.Title;
                vm.NextLectureUnlocked = next.CanOpen;
            }

            return new LectureWatchResult(LectureWatchOutcome.Ok, vm);
        }
    }
}
