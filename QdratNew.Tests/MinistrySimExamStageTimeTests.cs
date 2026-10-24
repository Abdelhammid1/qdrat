using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Exams.Implementations;
using Xunit;

namespace QdratNew.Tests
{
    // اختبار معمل القياس: IsStageTimeExpiredAsync هي مصدر الحقيقة (وقت الخادم) لمنع إنهاء القسم قبل انتهاء مدته
    // ولعرض ملخص نهاية الوقت — يجب ألا تقفل القسم أبدًا.
    public class MinistrySimExamStageTimeTests
    {
        private const int ExamId = 1;
        private const int AttemptId = 10;
        private const int Stage = 1;

        private static ApplicationDbContext CreateContext()
            => TestDbContextFactory.CreateFreshContext(out _);

        private static async Task SeedAsync(ApplicationDbContext db, DateTime? startedAt, bool isLocked = false, int durationMinutes = 26)
        {
            db.MinistrySimExamStages.Add(new MinistrySimExamStage
            {
                Id = 1, MinistrySimExamId = ExamId, StageNumber = Stage, DurationMinutes = durationMinutes
            });
            db.MinistrySimExamStudentAttempts.Add(new MinistrySimExamStudentAttempt
            {
                Id = AttemptId, MinistrySimExamId = ExamId, StudentId = 5, StartedAt = DateTime.Now
            });
            if (startedAt != null || isLocked)
            {
                db.MinistrySimExamStudentStageProgresses.Add(new MinistrySimExamStudentStageProgress
                {
                    MinistrySimExamStudentAttemptId = AttemptId, StageNumber = Stage,
                    StartedAt = startedAt, IsLocked = isLocked
                });
            }
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task WithinDuration_ReturnsFalse()
        {
            using var db = CreateContext();
            await SeedAsync(db, DateTime.Now.AddMinutes(-25));

            Assert.False(await new MinistrySimExamAttemptService(db).IsStageTimeExpiredAsync(AttemptId, Stage));
        }

        [Fact]
        public async Task AfterDuration_ReturnsTrue_AndDoesNotLockStage()
        {
            using var db = CreateContext();
            await SeedAsync(db, DateTime.Now.AddMinutes(-27));

            var expired = await new MinistrySimExamAttemptService(db).IsStageTimeExpiredAsync(AttemptId, Stage);

            Assert.True(expired);
            Assert.False(db.MinistrySimExamStudentStageProgresses.Single().IsLocked);
        }

        [Fact]
        public async Task UsesPerStageDuration_NotDefault26()
        {
            using var db = CreateContext();
            await SeedAsync(db, DateTime.Now.AddMinutes(-30), durationMinutes: 40);

            Assert.False(await new MinistrySimExamAttemptService(db).IsStageTimeExpiredAsync(AttemptId, Stage));
        }

        [Fact]
        public async Task AlreadyLockedStage_ReturnsFalse()
        {
            using var db = CreateContext();
            await SeedAsync(db, DateTime.Now.AddMinutes(-60), isLocked: true);

            Assert.False(await new MinistrySimExamAttemptService(db).IsStageTimeExpiredAsync(AttemptId, Stage));
        }

        [Fact]
        public async Task StageNotStarted_ReturnsFalse()
        {
            using var db = CreateContext();
            await SeedAsync(db, startedAt: null);

            Assert.False(await new MinistrySimExamAttemptService(db).IsStageTimeExpiredAsync(AttemptId, Stage));
        }

        [Fact]
        public async Task UnknownAttempt_ReturnsFalse()
        {
            using var db = CreateContext();
            await SeedAsync(db, DateTime.Now.AddMinutes(-60));

            Assert.False(await new MinistrySimExamAttemptService(db).IsStageTimeExpiredAsync(999, Stage));
        }
    }
}
