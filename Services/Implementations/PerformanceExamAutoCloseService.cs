using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Interfaces;
using QdratNew.Entities;

namespace QdratNew.Services.Implementations
{
    public class PerformanceExamAutoCloseService : IPerformanceExamAutoCloseService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IPerformanceIndicatorExamService _examService;
        private readonly IStudentExamStatisticsService _statsService;

        public PerformanceExamAutoCloseService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IPerformanceIndicatorExamService examService,
            IStudentExamStatisticsService statsService
        )
        {
            _contextFactory = contextFactory;
            _examService = examService;
            _statsService = statsService;
        }

        public async Task<bool> AutoCloseIfExpiredAsync(int studentId, int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🟢 تحميل حالة الطالب في الاختبار
            var studentExam = await _context.PerformanceIndicatorExamStudents
                .FirstOrDefaultAsync(x => x.StudentId == studentId &&
                                          x.PerformanceIndicatorExamId == examId);

            if (studentExam == null)
                return false;

            // لو الاختبار مُغلق سابقًا لا نكرر العملية
            if (studentExam.IsCompleted)
                return false;

            // لو لم يبدأ الطالب الاختبار
            if (studentExam.StartedAt == null)
                return false;

            // 🟢 بيانات الاختبار
            var exam = await _context.PerformanceIndicatorExams
                .FirstOrDefaultAsync(x => x.Id == examId);

            if (exam == null)
                return false;

            // 🕒 تحديد موعد الإغلاق
            DateTime mustEndAt = studentExam.StartedAt.Value.AddMinutes(exam.DurationMinutes);
            if (DateTime.UtcNow < mustEndAt)
                return false;

            // =====================================================
            // 🛑 غلق الاختبار رسميًا
            // =====================================================
            studentExam.IsCompleted = true;
            studentExam.CompletedAt = mustEndAt;
            await _context.SaveChangesAsync();

            // =====================================================
            // 🟦 جلب الأسئلة الخاصة بالاختبار
            // =====================================================
            var examQuestions = await _context.PerformanceIndicatorExamQuestions
                .Where(eq => eq.PerformanceIndicatorExamId == examId)
                .Select(eq => new { eq.SectionId, eq.QuestionId })
                .ToListAsync();

            if (!examQuestions.Any())
                return true;

            // =====================================================
            // 🟦 جلب محاولات الطالب
            // =====================================================
            var attempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId &&
                            a.PerformanceIndicatorExamId == examId)
                .ToListAsync();

            // =====================================================
            // 🟦 تجميع الأسئلة حسب المحور
            // =====================================================
            var sectionGroups = examQuestions
                .GroupBy(x => x.SectionId)
                .ToList();

            foreach (var sec in sectionGroups)
            {
                // تجاهل المحاور غير المعروفة
                if (sec.Key == null)
                    continue;

                int sectionId = sec.Key.Value;
                var qids = sec.Select(x => x.QuestionId).ToList();
                int totalQuestions = qids.Count;

                // ---------------------------
                // 🟢 إجابات صحيحة
                // ---------------------------
                int correct = attempts.Count(a =>
                    a.SectionId == sectionId &&
                    qids.Contains(a.QuestionId) &&
                    a.IsCorrect);

                // ---------------------------
                // 🟢 إجابات فعلية فقط
                // ---------------------------
                int answered = attempts.Count(a =>
                    a.SectionId == sectionId &&
                    qids.Contains(a.QuestionId) &&
                    !string.IsNullOrWhiteSpace(a.SelectedAnswer) &&
                    a.SelectedAnswer != "—");

                // ---------------------------
                // 🟢 غير محلولة
                // ---------------------------
                int skipped = totalQuestions - answered;

                // ---------------------------
                // 🟢 حساب النسبة
                // ---------------------------
                double percent = totalQuestions == 0 ? 0 :
                    Math.Round((correct * 100.0 / totalQuestions), 1);

                // ---------------------------
                // 🟢 حساب متوسط الوقت
                // ---------------------------
                double avgTime = attempts
                    .Where(a => a.SectionId == sectionId &&
                                qids.Contains(a.QuestionId) &&
                                a.TimeTakenSeconds > 0)
                    .Select(a => (double)a.TimeTakenSeconds)
                    .DefaultIfEmpty(0)
                    .Average();

                // =====================================================
                // 🟢 تسجيل نتيجة هذا المحور
                // =====================================================
                await _examService.RecordStudentResultAsync(
         studentId,
         examId,
         sectionId,
         percent,
         avgTime,
         rushed: false,
         unfocused: false
     );

            }

            // =====================================================
            // 🟦 تسجيل الأداء العام للطالب
            // =====================================================
            var stats = await _statsService.GetExamResultAsync(examId, studentId);

            var performance = new StudentPerformance
            {
                StudentID = studentId,
                Score = Math.Round(stats.ScorePercentage, 2),
                ExamDate = DateTime.UtcNow,
                ActivityType = QdratNew.Enums.PerformanceActivityType.PerformanceIndicatorExam,
                ExamType = QdratNew.Enums.ExamType.PerformanceScale,
                CurriculumId = exam.CurriculumId,
                StudyHours = (float)stats.SolveMinutes,
                EngagementScore = 100
            };

            _context.StudentPerformances.Add(performance);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
