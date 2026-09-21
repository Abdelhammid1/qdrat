using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Helpers
{
    public class ExamQuestionSelectorService : IExamQuestionSelectorService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ExamQuestionSelectorService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// ✅ اختيار الأسئلة بناءً على المنهج وعدد الأسئلة ومستويات الصعوبة.
        /// آمن تمامًا مع SQL Server 2014 (بدون أي WITH أو NEWID أو Contains).
        /// </summary>
        public async Task<List<Question>> SelectQuestionsAsync(
            int curriculumId,
            int total,
            int easy,
            int medium,
            int hard,
            QuestionUsageType targetUsageType,
            int studentId = 0,
            List<Guid>? previouslyUsedQuestionIds = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 1️⃣ جلب المحاور التابعة للمنهج
            var sectionIds = await _context.Sections
                .Where(s => s.CurriculumId == curriculumId)
                .Select(s => s.Id)
                .ToListAsync();

            if (!sectionIds.Any())
                return new List<Question>();

            // 2️⃣ جلب المؤشرات (الدروس) التابعة للمحاور
            var lessonIds = await _context.Lessons
                .Where(l => sectionIds.Contains(l.SectionId))
                .Select(l => l.Id)
                .ToListAsync();

            if (!lessonIds.Any())
                return new List<Question>();

            // 3️⃣ الأسئلة الأساسية المتاحة
            var baseQuestions = await _context.Questions
                .Where(q => lessonIds.Contains(q.LessonId)
                            && (q.UsageTypes & targetUsageType) == targetUsageType
                            && q.IsReviewed
                            && !q.IsRejected
                            && q.CorrectAnswer != null)
                .ToListAsync();

            // 4️⃣ استبعاد الأسئلة المستخدمة مسبقًا إن وجدت
            if (previouslyUsedQuestionIds != null && previouslyUsedQuestionIds.Any())
            {
                baseQuestions = baseQuestions
                    .Where(q => !previouslyUsedQuestionIds.Contains(q.Id))
                    .ToList();
            }

            if (!baseQuestions.Any())
                return new List<Question>();

            // 5️⃣ تقسيم الأسئلة حسب الصعوبة
            var easyQs = baseQuestions.Where(q => q.Difficulty == DifficultyLevel.Easy).ToList();
            var mediumQs = baseQuestions.Where(q => q.Difficulty == DifficultyLevel.Medium).ToList();
            var hardQs = baseQuestions.Where(q => q.Difficulty == DifficultyLevel.Hard).ToList();

            // 6️⃣ اختيار عشوائي داخل الذاكرة (بدون أي SQL ORDER)
            var rnd = new Random();

            List<Question> selected = new();

            selected.AddRange(easyQs.OrderBy(_ => rnd.Next()).Take(easy));
            selected.AddRange(mediumQs.OrderBy(_ => rnd.Next()).Take(medium));
            selected.AddRange(hardQs.OrderBy(_ => rnd.Next()).Take(hard));

            // 7️⃣ تكملة العدد لو ناقص
            if (selected.Count < total)
            {
                int remaining = total - selected.Count;
                var remainingQs = baseQuestions
                    .Except(selected)
                    .OrderBy(_ => rnd.Next())
                    .Take(remaining)
                    .ToList();
                selected.AddRange(remainingQs);
            }

            // 8️⃣ إزالة أي تكرارات
            selected = selected.GroupBy(q => q.Id).Select(g => g.First()).ToList();

            return selected;
        }
    }
}
