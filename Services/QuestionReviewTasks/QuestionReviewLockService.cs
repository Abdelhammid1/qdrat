using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;

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

        public IQueryable<Guid> HeldQuestionIds(ApplicationDbContext db)
        {
            return db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.IsLockActive || i.Status == QuestionReviewTaskItemStatus.Returned)
                .Select(i => i.QuestionId);
        }

        public async Task<QuestionHoldInfo?> GetHoldAsync(Guid questionId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            // الحجز النشط يسبق المرتجع؛ سؤال واحد قد يملك عدة عناصر تاريخية فنأخذ الأحدث
            return await db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.QuestionId == questionId
                            && (i.IsLockActive || i.Status == QuestionReviewTaskItemStatus.Returned))
                .OrderByDescending(i => i.IsLockActive)
                .ThenByDescending(i => i.Id)
                .Select(i => new QuestionHoldInfo(i.Id, i.TaskId, i.Task!.Code, i.Task.InstructorId, i.Status))
                .FirstOrDefaultAsync(ct);
        }
    }
}
