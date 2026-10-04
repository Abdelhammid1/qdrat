using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

namespace QdratNew.Services.QuestionReviewTasks
{
    public sealed class QuestionReviewLockService : IQuestionReviewLockService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

        public QuestionReviewLockService(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<ActiveLockInfo?> GetActiveLockAsync(Guid questionId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            return await db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.QuestionId == questionId && i.IsLockActive)
                .Select(i => new ActiveLockInfo(
                    i.Id,
                    i.TaskId,
                    i.Task!.Code,
                    i.Task.InstructorId,
                    i.Task.Instructor!.FullName))
                .FirstOrDefaultAsync(ct);
        }

        public IQueryable<Guid> LockedQuestionIds(ApplicationDbContext db)
        {
            return db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.IsLockActive)
                .Select(i => i.QuestionId);
        }
    }
}
