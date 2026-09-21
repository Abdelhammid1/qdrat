using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Question;
using System.Globalization;

namespace QdratNew.Services.AI
{
    public class AIQuestionAnalyzer
    {
        private readonly ApplicationDbContext _context;

        public AIQuestionAnalyzer(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<QuestionAIDashboardViewModel> AnalyzeAsync()
        {
            // 📊 إحصائيات عامة
            var total = await _context.Questions.CountAsync();
            var reviewed = await _context.Questions.CountAsync(q => q.IsReviewed);
            var unanswered = await _context.Questions.CountAsync(q => string.IsNullOrWhiteSpace(q.CorrectAnswer));

            // 📈 توزيع حسب مستوى الصعوبة
            var difficultyStats = await _context.Questions
                .GroupBy(q => q.Difficulty.ToString())
                .Select(g => new { Difficulty = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Difficulty, g => g.Count);

            // 📘 توزيع حسب المنهج
            var curriculumStats = await _context.Questions
                .Include(q => q.Curriculum)
                .GroupBy(q => q.Curriculum.Title)
                .Select(g => new { Curriculum = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Curriculum, g => g.Count);

            // 🗓 إحصائيات شهرية
            var monthlyStats = await _context.Questions
            .GroupBy(q => q.CreatedAt.Month)
            .Select(g => new MonthlyQuestionStat
            {
                Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key),
                Count = g.Count()
            })
            .ToListAsync(); // ⬅️ جلب البيانات أولاً

            monthlyStats = monthlyStats
                .OrderBy(x => DateTime.ParseExact(x.Month, "MMMM", CultureInfo.CurrentCulture)) // ⬅️ ثم الترتيب
                .ToList();


            // 🤖 تحليل ذكي باستخدام محاولات الطلاب
            var smartInsights = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                .GroupBy(a => new { a.QuestionId, a.Question.Title })
                .Where(g => g.Count() >= 5)
                .Select(g => new AIQuestionInsight
                {
                    QuestionId = g.Key.QuestionId,
                    Title = g.Key.Title,
                    SuccessRate = (float)g.Count(x => x.IsCorrect) / g.Count() * 100,
                    AvgTimeToSolve = g.Average(x => x.TimeTakenSeconds),
                    AIComment = "" // سيتم توليده لاحقًا
                }).ToListAsync();

            // 💡 توليد توصيات AI
            foreach (var insight in smartInsights)
            {
                if (insight.SuccessRate < 40)
                    insight.AIComment = "❗️معدل نجاح منخفض – راجع وضوح السؤال أو صعوبته";
                else if (insight.AvgTimeToSolve > 45)
                    insight.AIComment = "⌛️يستغرق وقتًا طويلًا للحل – ربما يحتاج تبسيطًا أو تعديلًا في الصورة";
                else
                    insight.AIComment = "✅ أداء جيد – لا حاجة لتعديل فوري";
            }

            // 🧠 النتيجة النهائية
            return new QuestionAIDashboardViewModel
            {
                TotalQuestions = total,
                ReviewedQuestions = reviewed,
                UnansweredQuestions = unanswered,
                QuestionsPerDifficulty = difficultyStats ?? new(),
                QuestionsPerCurriculum = curriculumStats ?? new(),
                QuestionsOverTime = monthlyStats ?? new(),
                SmartInsights = smartInsights ?? new()
            };
        }
    }
}
