using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.AI;
using QdratNew.Entities;

namespace QdratNew.Services.AI
{
    public class AITipsService
    {
        private readonly ApplicationDbContext _context;

        public AITipsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<SmartRecommendation>> GenerateForStudentAsync(int studentId)
        {
            var tips = new List<SmartRecommendation>();

            // ✅ 1. تحليل أكثر المحاور ضعفًا في الإجابات
            var weakSections = await _context.StudentAnswers
                .Where(a => a.StudentId == studentId && !a.IsCorrect)
                .Include(a => a.Question)
                    .ThenInclude(q => q.Section)
                .GroupBy(a => a.Question.SectionId)
                .Select(g => new
                {
                    SectionId = g.Key,
                    Title = g.First().Question.Section.Title,
                    ErrorCount = g.Count()
                })
                .OrderByDescending(g => g.ErrorCount)
                .Take(2)
                .ToListAsync();

            foreach (var section in weakSections)
            {
                tips.Add(new SmartRecommendation
                {
                    Message = $"راجع محور {section.Title} – نسبة الخطأ فيه مرتفعة",
                    Icon = "📉"
                });
            }

            // ✅ 2. تحليل ضعف التفاعل أو الالتزام في الأداء العام
            var perf = await _context.StudentPerformances
                .Where(sp => sp.StudentID == studentId)
                .OrderByDescending(sp => sp.ExamDate)
                .FirstOrDefaultAsync();

            if (perf != null && perf.EngagementRate < 50)
            {
                tips.Add(new SmartRecommendation
                {
                    Message = $"معدل تفاعلك منخفض ({perf.EngagementRate}%) – حاول المشاركة أكثر أو حضور الجلسات",
                    Icon = "⏱️"
                });
            }

            // ✅ 3. قلة النشاط داخل الخطة العلاجية
            var recentActions = await _context.RemedialPlanInteractions
                .Where(i => i.StudentId == studentId && i.InteractionTime > DateTime.Now.AddDays(-7))
                .CountAsync();

            if (recentActions < 1)
            {
                tips.Add(new SmartRecommendation
                {
                    Message = "لم يتم رصد أي تفاعل مع الخطة العلاجية هذا الأسبوع",
                    Icon = "🚨"
                });
            }

            // ✅ 4. التقييمات السلبية للجلسات (اختياري)
            var lowRating = await _context.StudySessionRatings
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.RatedAt)
                .FirstOrDefaultAsync();

            if (lowRating != null && (lowRating.SessionBenefit < 3 || lowRating.TrainerClarity < 3))
            {
                tips.Add(new SmartRecommendation
                {
                    Message = "تقييمك الأخير للجلسة منخفض – يمكنك طلب جلسة بديلة أو مراجعة الشرح مع المدرب",
                    Icon = "💬"
                });
            }

            return tips;
        }
    }
}
