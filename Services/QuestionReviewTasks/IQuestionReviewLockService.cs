using QdratNew.Data;
using QdratNew.Enums;

namespace QdratNew.Services.QuestionReviewTasks
{
    public sealed record ActiveLockInfo(long ItemId, int TaskId, string TaskCode, int InstructorId, string InstructorName);

    /// <summary>
    /// QRT-S4.2: عنصر مهمة «يحتجز» السؤال عن المسار العام للمدربين:
    /// إما محجوز بانتظار المراجعة (Pending) أو مُرجَع للإدارة ولم يُعالَج بعد (Returned).
    /// </summary>
    public sealed record QuestionHoldInfo(
        long ItemId, int TaskId, string TaskCode, int InstructorId, QuestionReviewTaskItemStatus ItemStatus);

    public interface IQuestionReviewLockService
    {
        /// <summary>عنصر المهمة النشط الذي يحجز السؤال (أو null).</summary>
        Task<ActiveLockInfo?> GetActiveLockAsync(Guid questionId, CancellationToken ct = default);

        /// <summary>استعلام يُستخدم داخل LINQ لاستبعاد الأسئلة المحجوزة.</summary>
        IQueryable<Guid> LockedQuestionIds(ApplicationDbContext db);

        /// <summary>
        /// QRT-S4.2: الأسئلة المحتجزة عن المسار العام للمدرب (محجوزة Pending أو مُرجَعة Returned)،
        /// تُستخدم داخل LINQ كاستعلام فرعي.
        /// </summary>
        IQueryable<Guid> HeldQuestionIds(ApplicationDbContext db);

        /// <summary>QRT-S4.2: العنصر الذي يحتجز السؤال (أو null إن كان حرًا في المسار العام).</summary>
        Task<QuestionHoldInfo?> GetHoldAsync(Guid questionId, CancellationToken ct = default);
    }
}
