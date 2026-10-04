using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.ViewModels.QuestionReviewTasks;

namespace QdratNew.Services.QuestionReviewTasks
{
    /// <summary>
    /// QRT-S1: هيكل الخدمة + مولّد الكود + إعادة حساب العدادات (D10).
    /// بقية العمليات تُنفَّذ في Sprints لاحقة وتُرجع فشلًا صريحًا حتى ذلك الحين.
    /// </summary>
    public sealed class QuestionReviewTaskService : IQuestionReviewTaskService
    {
        private const string NotImplementedMessage = "هذه العملية غير متاحة بعد.";

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly ILogger<QuestionReviewTaskService> _logger;

        public QuestionReviewTaskService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            ILogger<QuestionReviewTaskService> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _logger = logger;
        }

        // ===== الأدمن (Sprint 2 / 6) =====
        public Task<OperationResult> CreateTaskAsync(CreateQuestionReviewTaskInput input, ReviewActor actor, CancellationToken ct = default)
            => Task.FromResult(OperationResult.Fail(NotImplementedMessage));

        public Task<OperationResult> CancelTaskAsync(int taskId, string reason, ReviewActor actor, CancellationToken ct = default)
            => Task.FromResult(OperationResult.Fail(NotImplementedMessage));

        public Task<OperationResult> CloseTaskAsync(int taskId, ReviewActor actor, CancellationToken ct = default)
            => Task.FromResult(OperationResult.Fail(NotImplementedMessage));

        public Task<OperationResult> ExtendDueAsync(int taskId, DateTime? newDueUtc, ReviewActor actor, CancellationToken ct = default)
            => Task.FromResult(OperationResult.Fail(NotImplementedMessage));

        public Task<OperationResult> RemoveItemsAsync(int taskId, IReadOnlyCollection<long> itemIds, ReviewActor actor, CancellationToken ct = default)
            => Task.FromResult(OperationResult.Fail(NotImplementedMessage));

        public Task<OperationResult> ReassignRemainingAsync(int taskId, int newInstructorId, ReviewActor actor, CancellationToken ct = default)
            => Task.FromResult(OperationResult.Fail(NotImplementedMessage));

        public Task<OperationResult> ResolveReturnedAsync(long itemId, ReturnResolution resolution, string? note, ReviewActor actor, CancellationToken ct = default)
            => Task.FromResult(OperationResult.Fail(NotImplementedMessage));

        // ===== المدرب (Sprint 3) =====
        public Task<OperationResult> ApproveItemsAsync(int instructorId, int taskId, IReadOnlyCollection<long> itemIds, ReviewActor actor, CancellationToken ct = default)
            => Task.FromResult(OperationResult.Fail(NotImplementedMessage));

        public Task<OperationResult> MarkEditedAndApprovedAsync(int instructorId, long itemId, ReviewActor actor, CancellationToken ct = default)
            => Task.FromResult(OperationResult.Fail(NotImplementedMessage));

        public Task<OperationResult> ReturnItemAsync(int instructorId, long itemId, string note, ReviewActor actor, CancellationToken ct = default)
            => Task.FromResult(OperationResult.Fail(NotImplementedMessage));

        // ===== مولّد الكود QRT-{yyyy}-{0000} (QRT-S1.5) =====
        // يُستدعى من CreateTaskAsync (Sprint 2). الفهرس الفريد على Code يحمي من التكرار.
        internal async Task<string> NextCodeAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var year = _time.GetUtcNow().Year;
            var prefix = $"QRT-{year}-";

            var last = await db.QuestionReviewTasks.AsNoTracking()
                .Where(t => t.Code.StartsWith(prefix))
                .OrderByDescending(t => t.Code)
                .Select(t => t.Code)
                .FirstOrDefaultAsync(ct);

            var n = 1;
            if (last is not null && int.TryParse(last.AsSpan(prefix.Length), out var lastNumber))
                n = lastNumber + 1;

            return $"{prefix}{n:0000}";
        }

        // ===== إعادة حساب العدادات بـ GROUP BY واحد (D10) =====
        public async Task RecalculateCountersAsync(ApplicationDbContext db, int taskId, CancellationToken ct = default)
        {
            var groups = await db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.TaskId == taskId)
                .GroupBy(i => i.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            int CountOf(params QuestionReviewTaskItemStatus[] statuses)
                => groups.Where(g => statuses.Contains(g.Status)).Sum(g => g.Count);

            var task = await db.QuestionReviewTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
            if (task is null)
            {
                _logger.LogWarning("RecalculateCounters: task {TaskId} not found", taskId);
                return;
            }

            task.TotalItems = groups.Sum(g => g.Count);
            task.PendingItems = CountOf(QuestionReviewTaskItemStatus.Pending);
            task.ApprovedItems = CountOf(
                QuestionReviewTaskItemStatus.Approved,
                QuestionReviewTaskItemStatus.EditedAndApproved,
                QuestionReviewTaskItemStatus.ApprovedByAdmin);
            task.ReturnedItems = CountOf(QuestionReviewTaskItemStatus.Returned);
            task.RemovedItems = CountOf(QuestionReviewTaskItemStatus.Removed);

            await db.SaveChangesAsync(ct);
        }
    }
}
