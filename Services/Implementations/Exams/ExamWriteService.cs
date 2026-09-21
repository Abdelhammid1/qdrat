using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.DTOs.Exams;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces.Exams;

namespace QdratNew.Services.Implementations.Exams
{
    public class ExamWriteService : IExamWriteService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ExamWriteService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==================================
        // حفظ / تحديث محاولة سؤال (RAW)
        // ==================================
        public async Task SaveQuestionAttemptAsync(ExamQuestionAttemptInput input)
        {
            using var context = _contextFactory.CreateDbContext();

            var attempt = await context.QuestionAttemptNew
                .OrderByDescending(a => a.AttemptedAt)
                .FirstOrDefaultAsync(a =>
                    a.StudentId == input.StudentId &&
                    a.QuestionId == input.QuestionId &&
                    (
                        (input.ExamAssignmentId.HasValue && a.ExamAssignmentId == input.ExamAssignmentId) ||
                        (input.ExamAssignmentToStudentId.HasValue && a.ExamAssignmentToStudentId == input.ExamAssignmentToStudentId)
                    ));

            bool hasAnswer = !string.IsNullOrWhiteSpace(input.SelectedAnswer);

            if (attempt == null)
            {
                // إنشاء جديد فقط لو لا يوجد محاولة
                attempt = new QuestionAttemptNew
                {
                    StudentId = input.StudentId,
                    QuestionId = input.QuestionId,
                    ExamAssignmentId = input.ExamAssignmentId,
                    ExamAssignmentToStudentId = input.ExamAssignmentToStudentId,
                    SelectedAnswer = input.SelectedAnswer ?? string.Empty,
                    IsCorrect = input.IsCorrect,
                    TimeTakenSeconds = input.TimeTakenSeconds,
                    IsMarkedForReview = input.IsMarkedForReview,
                    AttemptedAt = DateTime.UtcNow
                };

                context.QuestionAttemptNew.Add(attempt);
            }
            else
            {
                // تحديث
                if (hasAnswer)
                {
                    attempt.SelectedAnswer = input.SelectedAnswer;
                    attempt.IsCorrect = input.IsCorrect;
                }

                if (input.TimeTakenSeconds > 0)
                    attempt.TimeTakenSeconds = input.TimeTakenSeconds;

                attempt.IsMarkedForReview = input.IsMarkedForReview;
                attempt.AttemptedAt = DateTime.UtcNow;

                context.QuestionAttemptNew.Update(attempt);
            }

            await context.SaveChangesAsync();
        }


        // ==================================
        // تسليم الاختبار (الحساب هنا فقط)
        // ==================================
        public async Task SubmitExamAsync(int examAssignmentId, int studentId)
        {
            using var context = _contextFactory.CreateDbContext();

            var attempts = await context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.StudentId == studentId &&
                    a.ExamAssignmentId == examAssignmentId)
                .ToListAsync();

            if (!attempts.Any())
                throw new InvalidOperationException("لا توجد محاولات مسجلة.");

            // ✅ DISTINCT QuestionId
            var perQuestion = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .ToList();

            int total = perQuestion.Count;
            int correct = perQuestion.Count(a => a.IsCorrect);

            double score = Math.Round(correct * 100.0 / total, 1);

            double effectiveTimeSeconds = perQuestion.Sum(a => a.TimeTakenSeconds);

            context.StudentPerformances.Add(new StudentPerformance
            {
                StudentID = studentId,
                ExamId = examAssignmentId,
                ExamType = ExamType.PerformanceScale,
                Score = score,
                ExamDate = DateTime.UtcNow,
                EngagementScore = effectiveTimeSeconds
            });

            await context.SaveChangesAsync();
        }
    }
}
