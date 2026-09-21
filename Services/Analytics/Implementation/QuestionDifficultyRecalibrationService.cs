using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Analytics.Interfaces;
using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.Services.Analytics.Implementation
{
    public class QuestionDifficultyRecalibrationService : IQuestionDifficultyRecalibrationService
    {
        private readonly ApplicationDbContext _context;

        public QuestionDifficultyRecalibrationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<QuestionDifficultyRecalibrationResultVM> PreviewAsync(
            QuestionDifficultyRecalibrationInputVM input)
        {
            return await CalculateAsync(input, applyChanges: false);
        }

        public async Task<QuestionDifficultyRecalibrationResultVM> ApplyAsync(
            QuestionDifficultyRecalibrationInputVM input)
        {
            return await CalculateAsync(input, applyChanges: true);
        }

        // ─────────────────────────────────────────────────────────────────
        // توزيع الأسئلة الحالي حسب الصعوبة لكل منهج
        // ─────────────────────────────────────────────────────────────────
        public async Task<List<CurriculumQuestionDistributionVM>> GetDistributionAsync(int? curriculumId)
        {
            var query = _context.Questions
                .AsNoTracking()
                .Where(q => !q.IsRejected);

            if (curriculumId.HasValue)
                query = query.Where(q => q.CurriculumId == curriculumId.Value);

            var data = await query
                .GroupBy(q => new { q.CurriculumId, q.Curriculum.Title })
                .Select(g => new CurriculumQuestionDistributionVM
                {
                    CurriculumId    = g.Key.CurriculumId,
                    CurriculumTitle = g.Key.Title ?? "",
                    EasyCount     = g.Count(q => q.Difficulty == DifficultyLevel.Easy),
                    MediumCount   = g.Count(q => q.Difficulty == DifficultyLevel.Medium),
                    HardCount     = g.Count(q => q.Difficulty == DifficultyLevel.Hard),
                    VeryHardCount = g.Count(q => q.Difficulty == DifficultyLevel.VeryHard),
                    Total         = g.Count()
                })
                .OrderBy(x => x.CurriculumTitle)
                .ToListAsync();

            return data;
        }

        // ─────────────────────────────────────────────────────────────────
        // Core logic — shared between Preview and Apply
        // ─────────────────────────────────────────────────────────────────
        private async Task<QuestionDifficultyRecalibrationResultVM> CalculateAsync(
            QuestionDifficultyRecalibrationInputVM input,
            bool applyChanges)
        {
            // 1. تحميل الأسئلة النشطة مع فلتر المنهج الاختياري
            var questionsQuery = _context.Questions
                .AsNoTracking()
                .Where(q => !q.IsRejected);

            if (input.CurriculumId.HasValue)
                questionsQuery = questionsQuery.Where(q => q.CurriculumId == input.CurriculumId.Value);

            var questions = await questionsQuery
                .Select(q => new
                {
                    q.Id,
                    q.Difficulty,
                    TitlePreview = q.Title != null && q.Title.Length > 80
                        ? q.Title.Substring(0, 80) + "…"
                        : q.Title ?? string.Empty
                })
                .ToListAsync();

            // 2. تحميل المحاولات مع فلتر التاريخ في SQL
            //    لا Contains هنا — فلتر بسيط على التاريخ فقط
            var attemptsQuery = _context.QuestionAttemptNew.AsNoTracking();

            if (input.FromDate.HasValue)
                attemptsQuery = attemptsQuery.Where(a => a.AttemptedAt >= input.FromDate.Value);

            if (input.ToDate.HasValue)
            {
                var toDateEnd = input.ToDate.Value.Date.AddDays(1);
                attemptsQuery = attemptsQuery.Where(a => a.AttemptedAt < toDateEnd);
            }

            var attempts = await attemptsQuery
                .Select(a => new { a.QuestionId, a.StudentId, a.IsCorrect })
                .ToListAsync();

            // 3. تجميع في الذاكرة بدون أي استعلام داخل حلقة
            var attemptsByQuestion = attempts
                .GroupBy(a => a.QuestionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 4. حساب التصنيف الجديد لكل سؤال
            var result = new QuestionDifficultyRecalibrationResultVM
            {
                TotalQuestions    = questions.Count,
                VeryHardMaxPercent = input.VeryHardMaxPercent,
                HardMaxPercent    = input.HardMaxPercent,
                MediumMaxPercent  = input.MediumMaxPercent,
                FromDate          = input.FromDate,
                ToDate            = input.ToDate,
                IsPreview         = !applyChanges
            };

            // قائمة التغييرات المحسوبة في الذاكرة
            var changes = new List<(Guid Id, DifficultyLevel OldDiff, DifficultyLevel NewDiff,
                                    double SuccessRate, int UniqueStudents, string Title)>();

            foreach (var q in questions)
            {
                if (!attemptsByQuestion.TryGetValue(q.Id, out var qAttempts) || qAttempts.Count == 0)
                {
                    result.SkippedNoData++;
                    continue;
                }

                // نحسب: كم طالب فريد حاول × كم منهم أجاب صحيح (ولو مرة واحدة)
                var uniqueStudents  = qAttempts.Select(a => a.StudentId).Distinct().Count();
                var correctStudents = qAttempts
                    .GroupBy(a => a.StudentId)
                    .Count(g => g.Any(a => a.IsCorrect));

                double successRate = uniqueStudents > 0
                    ? Math.Round((double)correctStudents / uniqueStudents * 100.0, 2)
                    : 0;

                var newDifficulty = ClassifyByRate(successRate, input);

                // تحديث عداد التوزيع
                switch (newDifficulty)
                {
                    case DifficultyLevel.VeryHard: result.VeryHardCount++; break;
                    case DifficultyLevel.Hard:     result.HardCount++;     break;
                    case DifficultyLevel.Medium:   result.MediumCount++;   break;
                    default:                       result.EasyCount++;     break;
                }

                changes.Add((q.Id, q.Difficulty, newDifficulty, successRate, uniqueStudents, q.TitlePreview));
            }

            // فقط الأسئلة التي سيتغير تصنيفها
            var changed = changes
                .Where(c => c.OldDiff != c.NewDiff)
                .ToList();

            result.UpdatedCount = changed.Count;

            // أعلى 100 تغيير للعرض
            result.ChangedItems = changed
                .OrderBy(c => c.SuccessRate)
                .Take(100)
                .Select(c => new QuestionDifficultyChangeItemVM
                {
                    QuestionId     = c.Id,
                    QuestionTitle  = c.Title,
                    OldDifficulty  = c.OldDiff,
                    NewDifficulty  = c.NewDiff,
                    SuccessRate    = Math.Round(c.SuccessRate, 1),
                    UniqueStudents = c.UniqueStudents
                })
                .ToList();

            // 5. تطبيق التغييرات إذا لم تكن معاينة فقط
            if (applyChanges && changed.Count > 0)
            {
                // نبني كيانات stub تحتوي فقط على Id + Difficulty الجديد
                // BulkUpdate سيستخدم PK ويحدث فقط العمود المحدد
                var changedDict = changed.ToDictionary(c => c.Id, c => c.NewDiff);

                var stubs = changedDict
                    .Select(kv => new Question { Id = kv.Key, Difficulty = kv.Value })
                    .ToList();

                await _context.BulkUpdateAsync(stubs, new BulkConfig
                {
                    PropertiesToInclude = new List<string> { nameof(Question.Difficulty) }
                });
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        private static DifficultyLevel ClassifyByRate(
            double successRate,
            QuestionDifficultyRecalibrationInputVM input)
        {
            if (successRate <= input.VeryHardMaxPercent) return DifficultyLevel.VeryHard;
            if (successRate <= input.HardMaxPercent)     return DifficultyLevel.Hard;
            if (successRate <= input.MediumMaxPercent)   return DifficultyLevel.Medium;
            return DifficultyLevel.Easy;
        }
    }
}
