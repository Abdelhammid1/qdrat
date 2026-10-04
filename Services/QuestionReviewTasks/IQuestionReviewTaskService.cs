using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.ViewModels.QuestionReviewTasks;

namespace QdratNew.Services.QuestionReviewTasks
{
    public sealed record ReviewActor(string UserId, string Name, UserRoleType Role);

    public sealed record OperationResult(bool Success, string Message, object? Data = null)
    {
        public static OperationResult Ok(string m, object? d = null) => new(true, m, d);
        public static OperationResult Fail(string m) => new(false, m);
    }

    public enum ReturnResolution { ApproveAsIs = 1, ReleaseToPool = 2, Reject = 3 }  // EditAndApprove يتم عبر شاشة التعديل ثم ApproveAsIs

    public interface IQuestionReviewTaskService
    {
        // الأدمن
        Task<OperationResult> CreateTaskAsync(CreateQuestionReviewTaskInput input, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> CancelTaskAsync(int taskId, string reason, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> CloseTaskAsync(int taskId, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> ExtendDueAsync(int taskId, DateTime? newDueUtc, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> RemoveItemsAsync(int taskId, IReadOnlyCollection<long> itemIds, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> ReassignRemainingAsync(int taskId, int newInstructorId, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> ResolveReturnedAsync(long itemId, ReturnResolution resolution, string? note, ReviewActor actor, CancellationToken ct = default);

        // المدرب (instructorId من الخادم دائمًا — D6)
        Task<OperationResult> ApproveItemsAsync(int instructorId, int taskId, IReadOnlyCollection<long> itemIds, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> MarkEditedAndApprovedAsync(int instructorId, long itemId, ReviewActor actor, CancellationToken ct = default);
        Task<OperationResult> ReturnItemAsync(int instructorId, long itemId, string note, ReviewActor actor, CancellationToken ct = default);

        Task RecalculateCountersAsync(ApplicationDbContext db, int taskId, CancellationToken ct = default);
    }
}
