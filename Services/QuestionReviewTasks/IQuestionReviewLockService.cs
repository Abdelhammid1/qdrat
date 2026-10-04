using QdratNew.Data;

namespace QdratNew.Services.QuestionReviewTasks
{
    public sealed record ActiveLockInfo(long ItemId, int TaskId, string TaskCode, int InstructorId, string InstructorName);

    public interface IQuestionReviewLockService
    {
        /// <summary>عنصر المهمة النشط الذي يحجز السؤال (أو null).</summary>
        Task<ActiveLockInfo?> GetActiveLockAsync(Guid questionId, CancellationToken ct = default);

        /// <summary>استعلام يُستخدم داخل LINQ لاستبعاد الأسئلة المحجوزة.</summary>
        IQueryable<Guid> LockedQuestionIds(ApplicationDbContext db);
    }
}
