using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.QuestionReviewTasks;

namespace QdratNew.Services.QuestionReviewTasks
{
    public sealed class QuestionReviewTaskQueryService : IQuestionReviewTaskQueryService
    {
        private const int MaxPageSize = 100;
        private static readonly TimeSpan BadgeTtl = TimeSpan.FromSeconds(60);

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly IMemoryCache _cache;

        public QuestionReviewTaskQueryService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            IMemoryCache cache)
        {
            _dbFactory = dbFactory;
            _time = time;
            _cache = cache;
        }

        public static string ItemStatusLabel(QuestionReviewTaskItemStatus s) => s switch
        {
            QuestionReviewTaskItemStatus.Pending => "بانتظار المراجعة",
            QuestionReviewTaskItemStatus.Approved => "اعتمدته",
            QuestionReviewTaskItemStatus.EditedAndApproved => "عُدِّل واعتُمد",
            QuestionReviewTaskItemStatus.Returned => "أُرجع للإدارة",
            QuestionReviewTaskItemStatus.ApprovedByAdmin => "اعتمدته الإدارة",
            QuestionReviewTaskItemStatus.Removed => "أُزيل من المهمة",
            QuestionReviewTaskItemStatus.ReturnResolved => "عولج الإرجاع",
            _ => string.Empty
        };

        private static ReviewTaskProgressVm ToProgress(int total, int pending, int approved, int returned, int removed)
        {
            var effective = QuestionReviewTaskMetrics.Effective(total, removed);
            return new ReviewTaskProgressVm
            {
                Total = total,
                Pending = pending,
                Approved = approved,
                Returned = returned,
                Removed = removed,
                Effective = effective,
                ApprovalPercent = QuestionReviewTaskMetrics.ApprovalPercent(approved, effective),
                HandledPercent = QuestionReviewTaskMetrics.HandledPercent(pending, effective)
            };
        }

        public async Task<InstructorTasksIndexVm> GetInstructorTasksAsync(
            int instructorId, QuestionReviewTaskStatus? status, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var nowUtc = _time.GetUtcNow().UtcDateTime;

            // مهام مدرب واحد: عددها محدود، والمرشّح يُطبَّق في الذاكرة لتبقى الأعداد الإجمالية ثابتة
            var all = await db.QuestionReviewTasks.AsNoTracking()
                .Where(t => t.InstructorId == instructorId)
                .Select(t => new
                {
                    t.Id, t.Code, t.Title, t.Status, t.Priority, t.DueAtUtc, t.CreatedAtUtc,
                    Curriculum = t.Curriculum != null ? t.Curriculum.Title : null,
                    t.TotalItems, t.PendingItems, t.ApprovedItems, t.ReturnedItems, t.RemovedItems
                })
                .ToListAsync(ct);

            var cards = all
                .Where(t => !status.HasValue || t.Status == status.Value)
                .Select(t => new InstructorTaskCardVm
                {
                    Id = t.Id,
                    Code = t.Code,
                    Title = t.Title,
                    CurriculumTitle = t.Curriculum,
                    Status = t.Status,
                    Priority = t.Priority,
                    DueLocal = QuestionReviewTaskMetrics.FormatLocal(t.DueAtUtc),
                    IsOverdue = QuestionReviewTaskMetrics.IsOverdue(t.Status, t.DueAtUtc, nowUtc),
                    AssignedLocal = QuestionReviewTaskMetrics.FormatLocal(t.CreatedAtUtc, "yyyy/MM/dd"),
                    Progress = ToProgress(t.TotalItems, t.PendingItems, t.ApprovedItems, t.ReturnedItems, t.RemovedItems)
                })
                // النشطة أولًا (المتأخرة ثم الأقرب موعدًا)، ثم بقية الحالات الأحدث أولًا
                .OrderBy(c => QuestionReviewTaskMetrics.IsActive(c.Status) ? 0 : 1)
                .ThenBy(c => c.IsOverdue ? 0 : 1)
                .ThenBy(c => c.DueLocal ?? "9999")
                .ThenByDescending(c => c.Id)
                .ToList();

            var active = all.Where(t => QuestionReviewTaskMetrics.IsActive(t.Status)).ToList();

            return new InstructorTasksIndexVm
            {
                StatusFilter = status,
                Tasks = cards,
                ActiveCount = active.Count,
                PendingQuestions = active.Sum(t => t.PendingItems),
                OverdueCount = active.Count(t => QuestionReviewTaskMetrics.IsOverdue(t.Status, t.DueAtUtc, nowUtc)),
                CompletedCount = all.Count(t => t.Status == QuestionReviewTaskStatus.Completed || t.Status == QuestionReviewTaskStatus.Closed)
            };
        }

        public async Task<InstructorTaskReviewVm?> GetInstructorTaskReviewAsync(
            int instructorId, int taskId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var nowUtc = _time.GetUtcNow().UtcDateTime;

            var t = await db.QuestionReviewTasks.AsNoTracking()
                .Where(x => x.Id == taskId && x.InstructorId == instructorId)
                .Select(x => new
                {
                    x.Id, x.Code, x.Title, x.AdminNote, x.Status, x.Priority, x.DueAtUtc,
                    Curriculum = x.Curriculum != null ? x.Curriculum.Title : null,
                    x.TotalItems, x.PendingItems, x.ApprovedItems, x.ReturnedItems, x.RemovedItems
                })
                .FirstOrDefaultAsync(ct);

            if (t is null)
                return null;

            return new InstructorTaskReviewVm
            {
                Id = t.Id,
                Code = t.Code,
                Title = t.Title,
                AdminNote = t.AdminNote,
                CurriculumTitle = t.Curriculum,
                Status = t.Status,
                Priority = t.Priority,
                DueLocal = QuestionReviewTaskMetrics.FormatLocal(t.DueAtUtc),
                IsOverdue = QuestionReviewTaskMetrics.IsOverdue(t.Status, t.DueAtUtc, nowUtc),
                CanAct = QuestionReviewTaskMetrics.IsActive(t.Status),
                Progress = ToProgress(t.TotalItems, t.PendingItems, t.ApprovedItems, t.ReturnedItems, t.RemovedItems)
            };
        }

        public async Task<ReviewItemsPage?> GetItemsPageAsync(
            int instructorId, int taskId, int start, int length, int? statusFilter, string? search, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var task = await db.QuestionReviewTasks.AsNoTracking()
                .Where(x => x.Id == taskId && x.InstructorId == instructorId)
                .Select(x => new { x.Status, x.TotalItems, x.PendingItems, x.ApprovedItems, x.ReturnedItems, x.RemovedItems })
                .FirstOrDefaultAsync(ct);

            if (task is null)
                return null;

            start = Math.Max(start, 0);
            length = length <= 0 ? 25 : Math.Min(length, MaxPageSize);
            var canAct = QuestionReviewTaskMetrics.IsActive(task.Status);

            var baseQuery = db.QuestionReviewTaskItems.AsNoTracking().Where(i => i.TaskId == taskId);
            var total = await baseQuery.CountAsync(ct);

            var query = baseQuery;
            if (statusFilter.HasValue)
            {
                var wanted = (QuestionReviewTaskItemStatus)statusFilter.Value;
                query = query.Where(i => i.Status == wanted);
            }

            var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            if (term is not null)
            {
                query = query.Where(i =>
                    (i.Question!.Title != null && i.Question.Title.Contains(term)) ||
                    (i.ReferenceNumberSnapshot != null && i.ReferenceNumberSnapshot.Contains(term)));
            }

            var filtered = ReferenceEquals(query, baseQuery) ? total : await query.CountAsync(ct);

            var rows = await query
                .OrderBy(i => i.SortOrder)
                .Skip(start)
                .Take(length)
                .Select(i => new
                {
                    i.Id,
                    i.QuestionId,
                    i.Status,
                    i.ReturnNote,
                    i.ReferenceNumberSnapshot,
                    Title = i.Question!.Title,
                    Section = i.Question.Section != null ? i.Question.Section.Title : null,
                    Lesson = i.Question.Lesson != null ? i.Question.Lesson.Title : null
                })
                .ToListAsync(ct);

            return new ReviewItemsPage
            {
                Total = total,
                Filtered = filtered,
                TaskStatus = task.Status,
                Progress = ToProgress(task.TotalItems, task.PendingItems, task.ApprovedItems, task.ReturnedItems, task.RemovedItems),
                Rows = rows.Select(r => new ReviewItemRowDto
                {
                    ItemId = r.Id,
                    QuestionId = r.QuestionId,
                    Reference = string.IsNullOrWhiteSpace(r.ReferenceNumberSnapshot) ? "—" : r.ReferenceNumberSnapshot!,
                    Title = r.Title ?? string.Empty,
                    Section = r.Section ?? "—",
                    Lesson = r.Lesson ?? "—",
                    Status = (int)r.Status,
                    StatusLabel = ItemStatusLabel(r.Status),
                    ReturnNote = r.ReturnNote,
                    CanAct = canAct && r.Status == QuestionReviewTaskItemStatus.Pending
                }).ToList()
            };
        }

        public async Task<Question?> GetPreviewQuestionAsync(int instructorId, long itemId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var questionId = await db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.Id == itemId && i.Task!.InstructorId == instructorId)
                .Select(i => (Guid?)i.QuestionId)
                .FirstOrDefaultAsync(ct);

            if (!questionId.HasValue)
                return null;

            return await db.Questions.AsNoTracking()
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .Include(q => q.Curriculum)
                .FirstOrDefaultAsync(q => q.Id == questionId.Value, ct);
        }

        public async Task<EditableTaskItem?> GetEditableItemAsync(int instructorId, long itemId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            return await db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.Id == itemId
                            && i.Status == QuestionReviewTaskItemStatus.Pending
                            && i.IsLockActive
                            && i.Task!.InstructorId == instructorId
                            && (i.Task.Status == QuestionReviewTaskStatus.Assigned || i.Task.Status == QuestionReviewTaskStatus.InProgress))
                .Select(i => new EditableTaskItem(i.Id, i.QuestionId, i.TaskId, i.Task!.Code))
                .FirstOrDefaultAsync(ct);
        }

        public async Task<int> GetPendingCountByUserIdAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return 0;

            return await _cache.GetOrCreateAsync(CacheKey(userId), async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = BadgeTtl;

                await using var db = await _dbFactory.CreateDbContextAsync(ct);
                return await db.QuestionReviewTasks.AsNoTracking()
                    .Where(t => t.Instructor!.UserId == userId
                                && (t.Status == QuestionReviewTaskStatus.Assigned || t.Status == QuestionReviewTaskStatus.InProgress))
                    .SumAsync(t => (int?)t.PendingItems, ct) ?? 0;
            });
        }

        public void InvalidatePendingCount(string userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
                _cache.Remove(CacheKey(userId));
        }

        private static string CacheKey(string userId) => $"qrt:pending:{userId}";
    }
}
