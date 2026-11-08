using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>
    /// OLC-S4.3 — متابعة الشريك للقراءة فقط (R19): دفعاته الأونلاين ← محاضراتها ذات المحتوى المنشور ← تقدّم طلابه.
    /// النطاق دائمًا partnerId من سياق الشريك (Session بعد تحقق UserPartners في PartnerBaseController) — لا يُقبل من الطلب.
    /// دفعة/محاضرة لا تخص الشريك = null (404) بلا تمييز يكشف الوجود.
    /// </summary>
    public interface IPartnerLearningContentService
    {
        Task<IReadOnlyList<PartnerLearningBatchVm>> GetBatchesAsync(int partnerId, CancellationToken ct = default);

        Task<PartnerLearningBatchDetailsVm?> GetBatchAsync(int partnerId, int batchId, CancellationToken ct = default);

        Task<PartnerLearningLectureDetailsVm?> GetLectureAsync(int partnerId, int lectureContentId, CancellationToken ct = default);
    }

    public sealed class PartnerLearningContentService : IPartnerLearningContentService
    {
        private const string ActiveEnrollmentStatus = "Active";

        private readonly ApplicationDbContext _db;

        public PartnerLearningContentService(ApplicationDbContext db)
        {
            _db = db;
        }

        private sealed record StudentRow(int StudentId, string FullName);

        private sealed record ContentRow(int Id, int LectureId, string LectureTitle, DateTime LectureDate, TimeSpan? ScheduledTime,
            string? SectionTitle, string Title, LectureContentMode Mode, int? DurationSeconds, int ItemsCount);

        private sealed record FullProgressRow(int ContentId, int StudentId, bool IsCompleted, double WatchedSeconds, int? DurationSeconds, DateTime? LastPingAtUtc);

        private sealed record ItemProgressRow(int ContentId, int StudentId, int CompletedItems, DateTime? LastPingAtUtc);

        // ═══════════════════════════ الدفعات ═══════════════════════════

        public async Task<IReadOnlyList<PartnerLearningBatchVm>> GetBatchesAsync(int partnerId, CancellationToken ct = default)
        {
            if (partnerId <= 0)
                return Array.Empty<PartnerLearningBatchVm>();

            var batches = await PartnerOnlineBatches(partnerId)
                .OrderBy(b => b.Name)
                .Select(b => new PartnerLearningBatchVm
                {
                    BatchId = b.Id,
                    Name = b.Name,
                    CourseName = b.Course != null ? b.Course.Name : null,
                    StudentsCount = _db.StudentBatchEnrollments.Count(e =>
                        e.BatchId == b.Id &&
                        e.Status == ActiveEnrollmentStatus &&
                        e.Student.IsActiveForLearning &&
                        e.Student.Branch.PartnerId == partnerId),
                    PublishedCount = _db.LectureContents.Count(c =>
                        c.Lecture.BatchId == b.Id && c.Status == LectureContentStatus.Published)
                })
                .ToListAsync(ct);

            if (batches.Count == 0)
                return batches;

            // نسبة الإتمام: FullVideo من LectureContentProgress، وIndicatorVideos = كل العناصر النشطة مكتملة.
            // استعلامان على مستوى الشريك كله (لا استعلام لكل دفعة)، مقيّدان بطلاب الشريك النشطين في الدفعة نفسها.
            var fullCompleted = await (
                    from p in _db.LectureContentProgress.AsNoTracking()
                    join c in _db.LectureContents.AsNoTracking() on p.LectureContentId equals c.Id
                    join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                    where c.Status == LectureContentStatus.Published
                       && c.ContentMode == LectureContentMode.FullVideo
                       && p.IsCompleted
                       && l.Batch.IsOnlineLearning && !l.Batch.IsDeleted
                       && l.Batch.Branch.PartnerId == partnerId
                       && _db.StudentBatchEnrollments.Any(e => e.BatchId == l.BatchId && e.StudentID == p.StudentId
                            && e.Status == ActiveEnrollmentStatus && e.Student.IsActiveForLearning && e.Student.Branch.PartnerId == partnerId)
                    group p by l.BatchId into g
                    select new { BatchId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var itemCompleted = await (
                    from ip in _db.LectureContentItemProgress.AsNoTracking()
                    join i in _db.LectureContentItems.AsNoTracking() on ip.LectureContentItemId equals i.Id
                    join c in _db.LectureContents.AsNoTracking() on i.LectureContentId equals c.Id
                    join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                    where c.Status == LectureContentStatus.Published
                       && c.ContentMode == LectureContentMode.IndicatorVideos
                       && ip.IsCompleted
                       && l.Batch.IsOnlineLearning && !l.Batch.IsDeleted
                       && l.Batch.Branch.PartnerId == partnerId
                       && _db.StudentBatchEnrollments.Any(e => e.BatchId == l.BatchId && e.StudentID == ip.StudentId
                            && e.Status == ActiveEnrollmentStatus && e.Student.IsActiveForLearning && e.Student.Branch.PartnerId == partnerId)
                    group ip by new { l.BatchId, c.Id, ip.StudentId } into g
                    select new { g.Key.BatchId, ContentId = g.Key.Id, Completed = g.Count() })
                .ToListAsync(ct);

            var itemsPerContent = itemCompleted.Count == 0
                ? new Dictionary<int, int>()
                : await (
                        from c in _db.LectureContents.AsNoTracking()
                        join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                        where c.Status == LectureContentStatus.Published
                           && c.ContentMode == LectureContentMode.IndicatorVideos
                           && l.Batch.Branch.PartnerId == partnerId
                        select new { c.Id, Items = c.Items.Count() })
                    .ToDictionaryAsync(x => x.Id, x => x.Items, ct);

            var fullByBatch = fullCompleted.ToDictionary(x => x.BatchId, x => x.Count);
            var itemByBatch = itemCompleted
                .Where(x => itemsPerContent.TryGetValue(x.ContentId, out var total) && total > 0 && x.Completed >= total)
                .GroupBy(x => x.BatchId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var b in batches)
                b.CompletedPairs = fullByBatch.GetValueOrDefault(b.BatchId) + itemByBatch.GetValueOrDefault(b.BatchId);

            return batches;
        }

        public async Task<PartnerLearningBatchDetailsVm?> GetBatchAsync(int partnerId, int batchId, CancellationToken ct = default)
        {
            var batch = await PartnerOnlineBatches(partnerId)
                .Where(b => b.Id == batchId)
                .Select(b => new { b.Id, b.Name, CourseName = b.Course != null ? b.Course.Name : null })
                .FirstOrDefaultAsync(ct);

            if (batch is null)
                return null;

            var students = await LoadStudentsAsync(partnerId, batchId, ct);
            var contents = await LoadContentsAsync(batchId, null, ct);
            var (full, items) = await LoadProgressAsync(batchId, null, ct);
            var studentIds = students.Select(s => s.StudentId).ToHashSet();

            return new PartnerLearningBatchDetailsVm
            {
                BatchId = batch.Id,
                BatchName = batch.Name,
                CourseName = batch.CourseName,
                StudentsCount = students.Count,
                Lectures = contents.Select(c => ToLectureVm(c, studentIds, full, items)).ToList()
            };
        }

        public async Task<PartnerLearningLectureDetailsVm?> GetLectureAsync(int partnerId, int lectureContentId, CancellationToken ct = default)
        {
            var owner = await (
                    from c in _db.LectureContents.AsNoTracking()
                    join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                    where c.Id == lectureContentId
                       && c.Status == LectureContentStatus.Published
                       && l.Batch.IsOnlineLearning && !l.Batch.IsDeleted
                       && l.Batch.Branch.PartnerId == partnerId
                    select new { l.BatchId, BatchName = l.Batch.Name })
                .FirstOrDefaultAsync(ct);

            if (owner is null || partnerId <= 0)
                return null;

            var students = await LoadStudentsAsync(partnerId, owner.BatchId, ct);
            var content = (await LoadContentsAsync(owner.BatchId, lectureContentId, ct)).FirstOrDefault();
            if (content is null)
                return null;

            var (full, items) = await LoadProgressAsync(owner.BatchId, lectureContentId, ct);
            var studentIds = students.Select(s => s.StudentId).ToHashSet();

            var fullByStudent = full.Where(p => p.ContentId == content.Id).ToDictionary(p => p.StudentId);
            var itemsByStudent = items.Where(p => p.ContentId == content.Id).ToDictionary(p => p.StudentId);

            var rows = students
                .Select(s =>
                {
                    var row = new PartnerLearningStudentRowVm { StudentId = s.StudentId, FullName = s.FullName };
                    if (content.Mode == LectureContentMode.FullVideo)
                    {
                        if (fullByStudent.TryGetValue(s.StudentId, out var p))
                        {
                            row.Started = true;
                            row.IsCompleted = p.IsCompleted;
                            row.ProgressPercent = FullPercent(p, content.DurationSeconds);
                            row.LastActivityUtc = p.LastPingAtUtc;
                        }
                    }
                    else if (itemsByStudent.TryGetValue(s.StudentId, out var ip))
                    {
                        row.Started = true;
                        row.CompletedItems = Math.Min(ip.CompletedItems, content.ItemsCount);
                        row.IsCompleted = content.ItemsCount > 0 && ip.CompletedItems >= content.ItemsCount;
                        row.ProgressPercent = content.ItemsCount == 0 ? 0 : (int)Math.Floor(row.CompletedItems * 100.0 / content.ItemsCount);
                        row.LastActivityUtc = ip.LastPingAtUtc;
                    }
                    return row;
                })
                .OrderByDescending(r => r.IsCompleted)
                .ThenByDescending(r => r.ProgressPercent)
                .ThenBy(r => r.FullName)
                .ToList();

            return new PartnerLearningLectureDetailsVm
            {
                BatchId = owner.BatchId,
                BatchName = owner.BatchName,
                Lecture = ToLectureVm(content, studentIds, full, items),
                Students = rows
            };
        }

        // ═══════════════════════════ مساعدات ═══════════════════════════

        private IQueryable<Entities.Batch> PartnerOnlineBatches(int partnerId)
            => _db.Batches.AsNoTracking()
                .Where(b => !b.IsDeleted && b.IsOnlineLearning && b.Branch.PartnerId == partnerId);

        // طلاب الشريك النشطون في الدفعة (نفس تعريف APD: تسجيل نشط + طالب نشط للتعلّم + فرع الطالب يخص الشريك)
        private async Task<List<StudentRow>> LoadStudentsAsync(int partnerId, int batchId, CancellationToken ct)
        {
            var rows = await _db.StudentBatchEnrollments.AsNoTracking()
                .Where(e => e.BatchId == batchId
                         && e.Status == ActiveEnrollmentStatus
                         && e.Student.IsActiveForLearning
                         && e.Student.Branch.PartnerId == partnerId)
                .Select(e => new { e.StudentID, e.Student.FullName })
                .ToListAsync(ct);

            return rows
                .GroupBy(r => r.StudentID)
                .Select(g => new StudentRow(g.Key, g.First().FullName ?? string.Empty))
                .ToList();
        }

        private async Task<List<ContentRow>> LoadContentsAsync(int batchId, int? contentId, CancellationToken ct)
        {
            var query =
                from c in _db.LectureContents.AsNoTracking()
                join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                where l.BatchId == batchId && c.Status == LectureContentStatus.Published
                select new { c, l };

            if (contentId.HasValue)
                query = query.Where(x => x.c.Id == contentId.Value);

            var rows = await query
                .OrderBy(x => x.l.Date)
                .ThenBy(x => x.l.ScheduledTime)
                .ThenBy(x => x.l.Id)
                .Select(x => new ContentRow(
                    x.c.Id, x.l.Id, x.l.Title, x.l.Date, x.l.ScheduledTime,
                    x.l.Section != null ? x.l.Section.Title : null,
                    x.c.Title, x.c.ContentMode, x.c.DurationSeconds,
                    x.c.Items.Count()))
                .ToListAsync(ct);

            return rows;
        }

        private async Task<(List<FullProgressRow> Full, List<ItemProgressRow> Items)> LoadProgressAsync(int batchId, int? contentId, CancellationToken ct)
        {
            var fullQuery =
                from p in _db.LectureContentProgress.AsNoTracking()
                join c in _db.LectureContents.AsNoTracking() on p.LectureContentId equals c.Id
                join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                where l.BatchId == batchId && c.Status == LectureContentStatus.Published
                      && c.ContentMode == LectureContentMode.FullVideo
                select new { p, c.Id };

            if (contentId.HasValue)
                fullQuery = fullQuery.Where(x => x.Id == contentId.Value);

            var full = await fullQuery
                .Select(x => new FullProgressRow(x.Id, x.p.StudentId, x.p.IsCompleted, x.p.WatchedSeconds, x.p.DurationSeconds, x.p.LastPingAtUtc))
                .ToListAsync(ct);

            var itemQuery =
                from ip in _db.LectureContentItemProgress.AsNoTracking()
                join i in _db.LectureContentItems.AsNoTracking() on ip.LectureContentItemId equals i.Id
                join c in _db.LectureContents.AsNoTracking() on i.LectureContentId equals c.Id
                join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                where l.BatchId == batchId && c.Status == LectureContentStatus.Published
                      && c.ContentMode == LectureContentMode.IndicatorVideos
                select new { ip, ContentId = c.Id };

            if (contentId.HasValue)
                itemQuery = itemQuery.Where(x => x.ContentId == contentId.Value);

            var items = await itemQuery
                .GroupBy(x => new { x.ContentId, x.ip.StudentId })
                .Select(g => new ItemProgressRow(
                    g.Key.ContentId,
                    g.Key.StudentId,
                    g.Sum(x => x.ip.IsCompleted ? 1 : 0),
                    g.Max(x => x.ip.LastPingAtUtc)))
                .ToListAsync(ct);

            return (full, items);
        }

        private static PartnerLearningLectureVm ToLectureVm(ContentRow c, HashSet<int> studentIds, List<FullProgressRow> full, List<ItemProgressRow> items)
        {
            int started, completed;
            if (c.Mode == LectureContentMode.FullVideo)
            {
                var rows = full.Where(p => p.ContentId == c.Id && studentIds.Contains(p.StudentId)).ToList();
                started = rows.Select(p => p.StudentId).Distinct().Count();
                completed = rows.Where(p => p.IsCompleted).Select(p => p.StudentId).Distinct().Count();
            }
            else
            {
                var rows = items.Where(p => p.ContentId == c.Id && studentIds.Contains(p.StudentId)).ToList();
                started = rows.Count;
                completed = c.ItemsCount == 0 ? 0 : rows.Count(p => p.CompletedItems >= c.ItemsCount);
            }

            return new PartnerLearningLectureVm
            {
                LectureContentId = c.Id,
                LectureTitle = c.LectureTitle ?? string.Empty,
                LectureDate = c.LectureDate,
                ScheduledTime = c.ScheduledTime,
                SectionTitle = string.IsNullOrWhiteSpace(c.SectionTitle) ? "غير محدد" : c.SectionTitle,
                Title = c.Title,
                Mode = c.Mode,
                ItemsCount = c.ItemsCount,
                StartedCount = started,
                CompletedCount = completed
            };
        }

        // رصيد المشاهدة بزمن الخادم مقابل المدة؛ غير المكتمل لا يُعرض 100%
        private static int FullPercent(FullProgressRow p, int? contentDuration)
        {
            if (p.IsCompleted)
                return 100;

            var duration = p.DurationSeconds ?? contentDuration;
            if (duration is not > 0)
                return 0;

            return Math.Min(99, (int)Math.Floor(p.WatchedSeconds * 100.0 / duration.Value));
        }
    }
}
