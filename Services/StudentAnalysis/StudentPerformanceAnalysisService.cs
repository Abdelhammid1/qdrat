using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.AI;
using QdratNew.ViewModels.AI;
using QdratNew.ViewModels.StudentAnalysis;

namespace QdratNew.Services.StudentAnalysis
{
    public class StudentPerformanceAnalysisService : IStudentPerformanceAnalysisService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAIClientService _ai;

        public StudentPerformanceAnalysisService(
            ApplicationDbContext context,
            IAIClientService ai)
        {
            _context = context;
            _ai = ai;
        }

        public async Task<StudentPerformanceReportVM> BuildStudentReportAsync(int studentId)
        {
            var raw = await ExtractRawData(studentId);
            var analytics = Analyze(raw);

            var aiResult = await _ai.AnalyzeStudentAsync(new AIPerformanceRequestVM
            {
                StudentId = raw.StudentId,
                LastExamScore = raw.LastExamScore,
                AvgBatchScore = raw.AvgBatchScore,
                WeakSections = raw.WeakSections,
                WrongQuestions = raw.WrongQuestions
            });

            return new StudentPerformanceReportVM
            {
                Raw = raw,
                Analytics = analytics,
                AI = new PerformanceAIResultVM
                {
                    AnalysisText = aiResult
                }
            };
        }

        // ===========================================================
        // استخراج البيانات الخام بدون Contains
        // ===========================================================
        private async Task<PerformanceRawDataVM> ExtractRawData(int studentId)
        {
            // 1) آخر نتيجة اختبار للطالب
            var lastExamScore = await _context.PerformanceIndicatorExamStudents
                .Where(x => x.StudentId == studentId && x.IsCompleted == true)
                .OrderByDescending(x => x.Id)
                .Select(x => x.ScorePercent ?? 0)
                .FirstOrDefaultAsync();

            // 2) الحصول على BatchId للطالب
            var batchId = await _context.StudentBatchEnrollments
                .Where(e => e.StudentID == studentId)
                .Select(e => e.BatchId)
                .FirstOrDefaultAsync();

            // إذا الطالب ليس له BatchId نرجع صفر
            if (batchId == 0)
            {
                return new PerformanceRawDataVM
                {
                    StudentId = studentId,
                    LastExamScore = lastExamScore,
                    AvgBatchScore = 0,
                    WrongQuestions = new List<string>(),
                    WeakSections = new List<string>()
                };
            }

            // 3) حساب متوسط درجات الدفعة باستخدام JOIN فقط
            var avgBatchScore = await (
                from e in _context.StudentBatchEnrollments
                join pes in _context.PerformanceIndicatorExamStudents
                    on e.StudentID equals pes.StudentId
                where e.BatchId == batchId
                      && pes.IsCompleted == true
                select pes.ScorePercent
            ).AverageAsync() ?? 0;

            // 4) الأسئلة الخاطئة للطالب
            var wrongQuestions = await (
                from qa in _context.QuestionAttemptNew
                where qa.StudentId == studentId && qa.IsCorrect == false
                select qa.QuestionId.ToString()
            ).ToListAsync();

            // 5) المحاور الضعيفة (JOIN فقط)
            var weakSections = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where a.StudentId == studentId && a.IsCorrect == false
                group s by s.Title into g
                orderby g.Count() descending
                select g.Key
            ).Take(3).ToListAsync();

            return new PerformanceRawDataVM
            {
                StudentId = studentId,
                LastExamScore = lastExamScore,
                AvgBatchScore = avgBatchScore,
                WrongQuestions = wrongQuestions,
                WeakSections = weakSections
            };
        }

        // ===========================================================
        // التحليل الرقمي الداخلي
        // ===========================================================
        private PerformanceAnalyticsVM Analyze(PerformanceRawDataVM raw)
        {
            double wrong = raw.WrongQuestions.Count;
            double strength = Math.Max(0, 100 - wrong);
            double weakness = wrong;

            double risk = (weakness + (raw.AvgBatchScore - raw.LastExamScore)) / 2;
            risk = Math.Clamp(risk, 0, 100);

            return new PerformanceAnalyticsVM
            {
                StrengthPercentage = strength,
                WeaknessPercentage = weakness,
                RiskLevel = risk,
                CriticalSections = raw.WeakSections
            };
        }
    }
}
