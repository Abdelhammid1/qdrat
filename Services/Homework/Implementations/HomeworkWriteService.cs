using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.DTOs.Homework;
using QdratNew.Entities;
using QdratNew.Services.Homework.Interfaces;

namespace QdratNew.Services.Homework.Implementations
{
    public class HomeworkWriteService : IHomeworkWriteService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public HomeworkWriteService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =====================================================
        // حفظ محاولة سؤال (NO AGGREGATION – NO CALCULATION)
        // =====================================================
        public async Task SaveQuestionAttemptAsync(HomeworkQuestionAttemptInput input)
        {
            using var context = _contextFactory.CreateDbContext();

            var existing = await context.QuestionAttemptNew
                .FirstOrDefaultAsync(x =>
                    x.StudentId == input.StudentId &&
                    x.HomeworkSetId == input.HomeworkSetId &&
                    x.QuestionId == input.QuestionId);

            if (existing == null)
            {
                existing = new QuestionAttemptNew
                {
                    StudentId = input.StudentId,
                    HomeworkSetId = input.HomeworkSetId,
                    HomeworkSetAttemptId = input.HomeworkSetAttemptId,
                    QuestionId = input.QuestionId
                };

                context.QuestionAttemptNew.Add(existing);
            }

            existing.SelectedAnswer = input.SelectedAnswer;
            existing.IsCorrect = input.IsCorrect;
            existing.TimeTakenSeconds = input.TimeTakenSeconds;
            existing.IsMarkedForReview = input.IsMarkedForReview;
            existing.AttemptedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
        }

        // =====================================================
        // تسليم الواجب (هنا فقط يتم الحساب مرة واحدة)
        // =====================================================
        public async Task SubmitHomeworkAsync(int homeworkSetId, int studentId)
        {
            using var context = _contextFactory.CreateDbContext();

            // 🔹 جلب كل محاولات الطالب لهذا الواجب
            var attempts = await context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.StudentId == studentId &&
                    a.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            if (!attempts.Any())
                throw new InvalidOperationException("لا توجد محاولات مسجلة لهذا الواجب.");

          

            var lastAttempts = attempts
                                .GroupBy(a => a.QuestionId)
                                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First());

            int totalQuestions = lastAttempts.Count();
            int correctAnswers = lastAttempts.Count(a => a.IsCorrect);


            double score = totalQuestions > 0
                ? Math.Round(correctAnswers * 100.0 / totalQuestions, 1)
                : 0;

            // 🔹 تحديث HomeworkSetStudent (THE SINGLE SOURCE FOR DASHBOARD)
            var record = await context.HomeworkSetStudents
                .FirstOrDefaultAsync(x =>
                    x.HomeworkSetId == homeworkSetId &&
                    x.StudentId == studentId);

            if (record == null)
                throw new InvalidOperationException("لم يتم العثور على سجل HomeworkSetStudent.");

            record.IsSubmitted = true;
            record.SubmittedAt = DateTime.UtcNow;
            record.Score = score;
            record.LastUpdated = DateTime.UtcNow;

            context.HomeworkSetStudents.Update(record);
            await context.SaveChangesAsync();
        }
    }
}
