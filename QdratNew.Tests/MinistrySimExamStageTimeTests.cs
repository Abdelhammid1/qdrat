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

        // فاصل الدقيقة بين الأقسام: البدء بعد انتهائه يُحتسب من لحظة انتهائه، فتبقى المدة الكلية 134 دقيقة وثوانٍ التحميل

        private static async Task SeedStagesAsync(ApplicationDbContext db, int count = 5)
        {
            for (var n = 1; n <= count; n++)
            {
                db.MinistrySimExamStages.Add(new MinistrySimExamStage
                {
                    Id = n, MinistrySimExamId = ExamId, StageNumber = n, DurationMinutes = 26
                });
            }
            db.MinistrySimExamStudentAttempts.Add(new MinistrySimExamStudentAttempt
            {
                Id = AttemptId, MinistrySimExamId = ExamId, StudentId = 5, StartedAt = DateTime.Now
            });
            await db.SaveChangesAsync();
        }

        private static async Task<DateTime> SeedFinishedFirstStageAsync(ApplicationDbContext db, TimeSpan sinceScheduledEnd, DateTime? endAt = null)
        {
            await SeedStagesAsync(db);
            var scheduledEnd = DateTime.Now - sinceScheduledEnd;
            db.MinistrySimExamStudentStageProgresses.Add(new MinistrySimExamStudentStageProgress
            {
                MinistrySimExamStudentAttemptId = AttemptId, StageNumber = 1,
                StartedAt = scheduledEnd.AddMinutes(-26), EndAt = endAt ?? scheduledEnd, IsLocked = true
            });
            await db.SaveChangesAsync();
            return scheduledEnd;
        }

        [Fact]
        public async Task StartStage_WithinBreak_StartsNow()
        {
            using var db = CreateContext();
            await SeedFinishedFirstStageAsync(db, TimeSpan.FromSeconds(20));

            var before = DateTime.Now;
            var progress = await new MinistrySimExamAttemptService(db).StartStageAsync(AttemptId, 2);

            Assert.True(progress.StartedAt >= before);
        }

        [Fact]
        public async Task StartStage_AfterBreak_CountsFromBreakDeadline()
        {
            using var db = CreateContext();
            var scheduledEnd = await SeedFinishedFirstStageAsync(db, TimeSpan.FromMinutes(10));

            var progress = await new MinistrySimExamAttemptService(db).StartStageAsync(AttemptId, 2);

            Assert.Equal(scheduledEnd.AddSeconds(MinistrySimExamAttemptService.StageBreakSeconds), progress.StartedAt);
        }

        [Fact]
        public async Task StartStage_FirstStage_HasNoBreakLimit()
        {
            using var db = CreateContext();
            await SeedStagesAsync(db);

            var before = DateTime.Now;
            var progress = await new MinistrySimExamAttemptService(db).StartStageAsync(AttemptId, 1);

            Assert.True(progress.StartedAt >= before);
        }

        [Fact]
        public async Task BreakRemaining_WithinBreak_ReturnsSecondsLeft()
        {
            using var db = CreateContext();
            await SeedFinishedFirstStageAsync(db, TimeSpan.FromSeconds(20));

            var remaining = await new MinistrySimExamAttemptService(db).GetBreakRemainingSecondsAsync(AttemptId, 1);

            Assert.InRange(remaining!.Value, 38, 40);
        }

        [Fact]
        public async Task BreakRemaining_StayingOnSummaryPastScheduledEnd_DoesNotExtendBreak()
        {
            using var db = CreateContext();
            await SeedFinishedFirstStageAsync(db, TimeSpan.FromMinutes(5), endAt: DateTime.Now);

            var remaining = await new MinistrySimExamAttemptService(db).GetBreakRemainingSecondsAsync(AttemptId, 1);

            Assert.Equal(0, remaining);
        }

        [Fact]
        public async Task BreakRemaining_LockedRightAfterStageEnd_GivesFullMinute()
        {
            using var db = CreateContext();
            await SeedFinishedFirstStageAsync(db, TimeSpan.FromSeconds(2), endAt: DateTime.Now);

            var remaining = await new MinistrySimExamAttemptService(db).GetBreakRemainingSecondsAsync(AttemptId, 1);

            Assert.InRange(remaining!.Value, 58, 60);
        }

        [Fact]
        public async Task BreakRemaining_StageNotStarted_ReturnsNull()
        {
            using var db = CreateContext();
            await SeedStagesAsync(db);

            Assert.Null(await new MinistrySimExamAttemptService(db).GetBreakRemainingSecondsAsync(AttemptId, 1));
        }

        [Fact]
        public async Task FullExam_LeftUnattended_ClosesAt134MinutesPlusLoadSeconds()
        {
            using var db = CreateContext();
            await SeedStagesAsync(db);
            var examStart = DateTime.Now.AddHours(-10);
            db.MinistrySimExamStudentStageProgresses.Add(new MinistrySimExamStudentStageProgress
            {
                MinistrySimExamStudentAttemptId = AttemptId, StageNumber = 1, StartedAt = examStart
            });
            await db.SaveChangesAsync();
            var service = new MinistrySimExamAttemptService(db);

            await service.LockStageAsync(AttemptId, 1, timeExpired: true);
            MinistrySimExamStudentStageProgress last = null!;
            for (var n = 2; n <= 5; n++)
            {
                last = await service.StartStageAsync(AttemptId, n);
                if (n < 5)
                    await service.LockStageAsync(AttemptId, n, timeExpired: true);
            }

            var expected = TimeSpan.FromMinutes(134) + TimeSpan.FromSeconds(4 * MinistrySimExamAttemptService.StageTransitionGraceSeconds);
            Assert.Equal(expected, last.StartedAt!.Value.AddMinutes(26) - examStart);
        }
    }
}
