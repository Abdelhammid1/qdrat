using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.ViewModels.Parents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Parents.Implementations
{
    public class AdaptiveQuestionPickerService : IAdaptiveQuestionPickerService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public AdaptiveQuestionPickerService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<Guid>> PickQuestionsAsync(AdaptiveQuestionPickRequest request)
        {
            using var db = _contextFactory.CreateDbContext();

            // الأسئلة التي أخطأ فيها الطالب سابقاً
            var wrongQuestionIds = await db.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.StudentId == request.StudentId && !a.IsCorrect)
                .OrderByDescending(a => a.AttemptedAt)
                .Take(50)
                .Select(a => a.QuestionId)
                .ToListAsync();

            var wrongSet = new HashSet<Guid>(wrongQuestionIds);

            // بناء الاستعلام الأساسي حسب المنهج/المحور
            var query = db.Questions
                .AsNoTracking()
                .Where(q => !request.CurriculumId.HasValue ||
                            q.Lesson.Section.CurriculumId == request.CurriculumId);

            if (request.SectionId.HasValue)
                query = query.Where(q => q.SectionId == request.SectionId);

            // جلب الأسئلة حسب الصعوبة
            var allQuestions = await query
                .Select(q => new { q.Id, q.Difficulty, q.SectionId })
                .ToListAsync();

            if (!allQuestions.Any())
                return new List<Guid>();

            // توزيع النسب حسب مستوى الطالب
            var (easyPct, medPct, hardPct) = GetDistribution(request.StudentLevel, request.PracticeMode);

            int total = Math.Min(request.QuestionCount, allQuestions.Count);
            int easyCount = (int)Math.Round(total * easyPct);
            int medCount = (int)Math.Round(total * medPct);
            int hardCount = total - easyCount - medCount;

            var rng = new Random();

            var easy = allQuestions
                .Where(q => q.Difficulty == DifficultyLevel.Easy)
                .OrderBy(_ => rng.Next())
                .Take(easyCount)
                .Select(q => q.Id);

            var medium = allQuestions
                .Where(q => q.Difficulty == DifficultyLevel.Medium)
                .OrderBy(_ => rng.Next())
                .Take(medCount)
                .Select(q => q.Id);

            // تفضيل أسئلة الأخطاء السابقة في الصعبة
            var hard = allQuestions
                .Where(q => q.Difficulty == DifficultyLevel.Hard || q.Difficulty == DifficultyLevel.VeryHard)
                .OrderByDescending(q => wrongSet.Contains(q.Id) ? 1 : 0)
                .ThenBy(_ => rng.Next())
                .Take(hardCount)
                .Select(q => q.Id);

            var picked = easy.Concat(medium).Concat(hard)
                .Distinct()
                .OrderBy(_ => rng.Next())
                .Take(total)
                .ToList();

            // إذا لم نصل للعدد المطلوب، نكمل من أي أسئلة متاحة
            if (picked.Count < total)
            {
                var pickedSet = new HashSet<Guid>(picked);
                var extra = allQuestions
                    .Where(q => !pickedSet.Contains(q.Id))
                    .OrderBy(_ => rng.Next())
                    .Take(total - picked.Count)
                    .Select(q => q.Id);
                picked.AddRange(extra);
            }

            return picked;
        }

        private static (double easy, double med, double hard) GetDistribution(string level, string mode)
        {
            if (mode == "ExamPreparation") return (0.15, 0.45, 0.40);
            if (mode == "Challenge") return (0.05, 0.35, 0.60);

            return level switch
            {
                "VeryWeak" => (0.60, 0.30, 0.10),
                "Weak" => (0.40, 0.45, 0.15),
                "Average" => (0.25, 0.50, 0.25),
                "Good" => (0.10, 0.40, 0.50),
                _ => (0.25, 0.50, 0.25)
            };
        }
    }
}
