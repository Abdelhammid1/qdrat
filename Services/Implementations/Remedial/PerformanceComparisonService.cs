using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Remedial;
using QdratNew.Enums;

namespace QdratNew.Services.Implementations.Remedial
{
    public class PerformanceComparisonService : IPerformanceComparisonService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public PerformanceComparisonService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // 🧩 تحليل المحاور الضعيفة للطالب في منهج محدد
        public async Task<List<WeakSectionVm>> AnalyzeWeakSectionsAsync(int studentId, int curriculumId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🧠 جلب أداء الطالب في كل محور داخل المنهج
            var sectionScores = await (
                from sp in _context.StudentPerformances
                join s in _context.Sections on sp.SectionId equals s.Id
                where sp.StudentID == studentId && s.CurriculumId == curriculumId
                group sp by new { s.Id, s.Title } into g
                select new WeakSectionVm
                {
                    SectionId = g.Key.Id,
                    SectionTitle = g.Key.Title,
                    ScorePercent = Math.Round(g.Average(x => x.Score), 1),
                    HasRemedialPlan = false
                }
            ).ToListAsync();

            // 🔹 تحديد المحاور الضعيفة (< 60%)
            var weakSections = sectionScores
                .Where(x => x.ScorePercent < 60)
                .OrderBy(x => x.ScorePercent)
                .ToList();

            // 🔹 تحديث حالة وجود خطة علاجية
            foreach (var ws in weakSections)
            {
                ws.HasRemedialPlan = await _context.RemedialPlans
                    .AnyAsync(p => p.StudentID == studentId && p.SectionId == ws.SectionId);
            }

            return weakSections;
        }

        // 🧩 تحليل الأداء التفصيلي داخل محور معين
        public async Task<List<StudentPerformanceComparisonVm>> AnalyzeStudentSectionPerformanceAsync(int studentId, int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var section = await _context.Sections
                .Include(s => s.Curriculum)
                .FirstOrDefaultAsync(s => s.Id == sectionId);

            if (section == null)
                throw new Exception("❌ لم يتم العثور على بيانات المحور.");

            // 🧠 جلب جميع الدروس (المؤشرات) داخل هذا المحور
            var lessons = await _context.Lessons
                .Where(l => l.SectionId == sectionId)
                .ToListAsync();

            var resultList = new List<StudentPerformanceComparisonVm>();

            foreach (var lesson in lessons)
            {
                // 🔹 المتوسط من جدول StudentPerformance (تجميع حسب نوع النشاط)
                var records = await _context.StudentPerformances
                    .Where(sp => sp.StudentID == studentId && sp.SectionId == sectionId)
                    .ToListAsync();

                double avgHomework = records
                    .Where(r => r.ActivityType == PerformanceActivityType.Homework)
                    .Select(r => r.Score)
                    .DefaultIfEmpty(0)
                    .Average();

                double avgExam = records
                    .Where(r => r.ActivityType == PerformanceActivityType.FinalExam)
                    .Select(r => r.Score)
                    .DefaultIfEmpty(0)
                    .Average();

                double avgIndicator = records
                    .Where(r => r.ActivityType == PerformanceActivityType.PerformanceIndicatorExam)
                    .Select(r => r.Score)
                    .DefaultIfEmpty(0)
                    .Average();

                var vm = new StudentPerformanceComparisonVm
                {
                    StudentId = studentId,
                    CurriculumTitle = section.Curriculum?.Title ?? "—",
                    SectionTitle = section.Title,
                    LessonTitle = lesson.Title,
                    AverageHomeworkScore = Math.Round(avgHomework, 1),
                    AverageExamScore = Math.Round(avgExam, 1),
                    AverageIndicatorScore = Math.Round(avgIndicator, 1),
                    Recommendation = GetSmartRecommendation(avgIndicator, avgHomework, avgExam)
                };

                resultList.Add(vm);
            }

            return resultList;
        }

        // 🧠 توصية ذكية بناءً على المقارنة
        private string GetSmartRecommendation(double indicator, double homework, double exam)
        {
            if (indicator < 60 && (homework < 60 || exam < 60))
                return "🚨 ضعف عام – يحتاج خطة علاجية مكثفة في هذا المحور.";
            if (indicator < exam && exam > 60)
                return "⚠️ ضعف في التطبيق النهائي، يُوصى بمراجعة الفيديوهات العلاجية.";
            if (indicator > 80 && exam < 60)
                return "🎯 أداء جيد في المؤشر النهائي مع ضعف سابق، يُوصى بالمراجعة الجزئية.";
            return "✅ أداء مستقر – لا يحتاج تدخل علاجي حالياً.";
        }
    }
}
