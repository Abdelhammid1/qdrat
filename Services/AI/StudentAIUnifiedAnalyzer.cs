using Microsoft.EntityFrameworkCore;
using QdratNew.AI.Analysis;
using QdratNew.AI.Trainers;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.AI;
using QdratNew.Services.Implementations;
using QdratNew.ViewModels.Students;

namespace QdratNew.Services.AI
{
    public class StudentAIUnifiedAnalyzer
    {
        private readonly ApplicationDbContext _context;
        private readonly StudentActivityAIAnalyzer _activityAnalyzer;
        private readonly StudentAIAnalysisService _mlPredictor;

        public StudentAIUnifiedAnalyzer(ApplicationDbContext context, StudentPerformanceTrainer trainer)
        {
            _context = context;
            _activityAnalyzer = new StudentActivityAIAnalyzer();
            _mlPredictor = new StudentAIAnalysisService(context, trainer);
        }

        public async Task<StudentAIUnifiedAnalysisViewModel> AnalyzeAsync(int studentId)
        {
            // 1) بيانات الأداء للطالب (بدون Include)
            var performances = await _context.StudentPerformances
                .AsNoTracking()
                .Where(p => p.StudentID == studentId)
                .ToListAsync();

            // إن كنتَ تحتاج خصائص من Section، اجلبها بجوين خفيف:
            // var perfWithSection = await (
            //     from p in _context.StudentPerformances.AsNoTracking()
            //     join s in _context.Sections.AsNoTracking() on p.SectionID equals s.Id
            //     where p.StudentID == studentId
            //     select new { Perf = p, SectionTitle = s.Title }
            // ).ToListAsync();

            // 2) سجلات النشاط
            var activityLogs = await _context.StudentActivityLogs
                .AsNoTracking()
                .Where(l => l.StudentId == studentId)
                .ToListAsync();

            // 3) تقارير الذكاء الاصطناعي (حسب خدماتك الحالية)
            var performanceReport = AIStudentPerformanceAnalyzer.Analyze(performances);
            var activityInsights = _activityAnalyzer.Analyze(activityLogs);
            var predictedText = _mlPredictor.AnalyzePerformanceLevel(studentId);

            // 4) الطالب (للبيانات العامة فقط)
            var student = await _context.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => new { s.StudentID })
                .FirstOrDefaultAsync();
            if (student == null) throw new InvalidOperationException("Student not found.");

            // 5) دفعات الطالب الحالية عبر جدول الربط Many-to-Many
            var myBatchIdsQuery = _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.StudentID == student.StudentID)
                .Select(e => e.BatchId);

            // 6) جميع أداء طلاب الدفعات نفسها (Avoid Contains: نستخدم JOIN على دفعات الطالب)
            var batchPerformances = await (
                from sp in _context.StudentPerformances.AsNoTracking()
                join e in _context.StudentBatchEnrollments.AsNoTracking()
                    on sp.StudentID equals e.StudentID
                join mb in myBatchIdsQuery
                    on e.BatchId equals mb
                select sp
            ).ToListAsync();

            var avgBatchSuccessRate = batchPerformances.Count == 0
                ? 0f
                : (float)batchPerformances.Count(sp => sp.Score >= 60) / batchPerformances.Count * 100f;

            // 7) متوسط وقت الواجبات لدفعات الطالب (JOIN بدلاً من h.Student.BatchId)
            var avgBatchHomeworkTime = await (
                from h in _context.Homeworks.AsNoTracking()
                join e in _context.StudentBatchEnrollments.AsNoTracking()
                    on h.StudentId equals e.StudentID
                join mb in myBatchIdsQuery
                    on e.BatchId equals mb
                where h.Status == HomeworkStatus.Submitted
                select (float?)h.TimeSpentSeconds
            ).AverageAsync() ?? 0f;

            // 8) تحليل مختصر إضافي
            var logicAnalyzer = new StudentAIAnalyzer();
            var aiSummary = logicAnalyzer.Analyze(new StudentAIAnalysisInput
            {
                SuccessRate = (float)performanceReport.SuccessRate,
                SessionCompletionRatio = 0.7f,  // TODO: حسّن القراءة من جلسات فعلية إن متاح
                AverageHomeworkTime = 800f,  // TODO: استبدل بالقيمة المحسوبة إن أردت
                TotalAssignments = performances.Count
            });

            // 9) قيمة تقديرية لخطر الدفعة (يمكنك تحسين المعادلة لاحقاً)
            var avgBatchRiskScore = 2f;

            // 10) تجميع المخرجات
            return new StudentAIUnifiedAnalysisViewModel
            {
                AnalyzedAt = DateTime.Now,
                AssessedLevel = aiSummary.AssessedLevel,
                RiskScore = aiSummary.RiskScore,
                Recommendations = aiSummary.Recommendations,
                SuccessRate = performanceReport.SuccessRate,
                AverageScore = performanceReport.AverageScore,
                WeakTopics = performanceReport.WeakTopics,
                SectionSummaries = performanceReport.SectionSummaries,
                ActivityInsights = activityInsights,
                AverageBatchSuccessRate = avgBatchSuccessRate,
                AverageBatchRiskScore = avgBatchRiskScore,
                AverageBatchHomeworkTime = avgBatchHomeworkTime,
                PredictedPerformanceComment = predictedText
            };
        }
    }
}
