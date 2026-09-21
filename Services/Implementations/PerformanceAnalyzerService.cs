using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Implementations
{
    /// <summary>
    /// ✅ PerformanceAnalyzerService
    /// - تحليل نتائج اختبار المؤشر
    /// - إنشاء خطة علاجية تلقائية عند الرسوب
    /// - متوافق تمامًا مع كيانات مشروع QdratNew
    /// </summary>
    public class PerformanceAnalyzerService : IPerformanceAnalyzerService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IMemoryCache _cache;
        private readonly IAdvancedNotificationService _notificationService;

        public PerformanceAnalyzerService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IMemoryCache cache,
            IAdvancedNotificationService notificationService)
        {
            _contextFactory = contextFactory;
            _cache = cache;
            _notificationService = notificationService;
        }
        // 🔹 تحليل كل نتائج الطلاب لاختبار محدد
        public async Task AnalyzeExamResultsAsync(int performanceIndicatorExamId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exam = await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .Include(e => e.Sections)
                .FirstOrDefaultAsync(e => e.Id == performanceIndicatorExamId);

            if (exam == null)
                throw new Exception("⚠️ اختبار المؤشر غير موجود.");

            var results = await _context.StudentIndicatorResults
                .AsNoTracking()
                .Where(r => r.PerformanceIndicatorExamId == performanceIndicatorExamId)
                .Include(r => r.Student)
                .ToListAsync();

            foreach (var result in results)
            {
                if (!result.IsPassed)
                {
                    var sectionDifficulty = await GetSectionDifficultyAsync(result.SectionId);
                    var averages = await GetStudentPerformanceAveragesAsync(result.StudentId, result.SectionId);

                    string reason = DetectFailureReason(
                        result.ScorePercent,
                        result.AverageTimePerQuestion ?? 0,
                        sectionDifficulty,
                        averages.HomeworkAvg,
                        averages.ExamAvg
                    );

                    await CreateRemedialPlanForStudentAsync(result.StudentId, result.SectionId, performanceIndicatorExamId, reason);
                }
            }
        }


        // 🔹 تحليل نتيجة طالب محدد (مستخدم في حالات فردية)
        public async Task AnalyzeStudentResultAsync(int studentId, int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var results = await _context.StudentIndicatorResults
                .AsNoTracking()
                .Where(r => r.PerformanceIndicatorExamId == examId && r.StudentId == studentId)
                .ToListAsync();

            foreach (var result in results.Where(r => !r.IsPassed))
            {
                var sectionDifficulty = await GetSectionDifficultyAsync(result.SectionId);
                var averages = await GetStudentPerformanceAveragesAsync(result.StudentId, result.SectionId);

                string reason = DetectFailureReason(
                    result.ScorePercent,
                    result.AverageTimePerQuestion ?? 0,
                    sectionDifficulty,
                    averages.HomeworkAvg,
                    averages.ExamAvg
                );

                await CreateRemedialPlanForStudentAsync(result.StudentId, result.SectionId, examId, reason);
            }
        }



        // 🔹 تحليل طالب محدد
        public async Task AnalyzeExamResultsAsync(int performanceIndicatorExamId, int? batchId = null, string? gender = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            // الأساس: جميع نتائج الطلاب لهذا الاختبار
            var query = _context.StudentIndicatorResults
                .AsNoTracking()
                .Include(r => r.Student)
                .Where(r => r.PerformanceIndicatorExamId == performanceIndicatorExamId);

            // ✅ فلترة بالدفعة باستخدام جدول الربط StudentBatchEnrollment
            if (batchId.HasValue)
            {
                query = query.Where(r =>
                    _context.StudentBatchEnrollments
                        .Any(e => e.StudentID == r.StudentId && e.BatchId == batchId.Value && e.Status == "Active"));
            }

            // ✅ فلترة بالجنس إذا كانت مطلوبة
            if (!string.IsNullOrEmpty(gender))
            {
                query = query.Where(r => r.Student.Gender.ToLower() == gender.ToLower());
            }

            var results = await query.ToListAsync();

            foreach (var result in results.Where(r => !r.IsPassed))
            {
                var sectionDifficulty = await GetSectionDifficultyAsync(result.SectionId);
                var averages = await GetStudentPerformanceAveragesAsync(result.StudentId, result.SectionId);
                string reason = DetectFailureReason(
                    result.ScorePercent,
                    result.AverageTimePerQuestion ?? 0,
                    sectionDifficulty,
                    averages.HomeworkAvg,
                    averages.ExamAvg);

                await CreateRemedialPlanForStudentAsync(result.StudentId, result.SectionId, performanceIndicatorExamId, reason);
            }
        }

        // 🔹 منطق التحليل الذكي
        public string DetectFailureReason(double scorePercent, double avgTimePerQuestion, double sectionDifficulty, double avgHomeworkScore, double avgExamScore)
        {
            if (avgHomeworkScore < 60 && avgExamScore < 60)
                return "ضعف في الفهم العام للمحور";
            if (avgExamScore > 70 && scorePercent < 50)
                return "تسرع أو قلة تركيز أثناء الاختبار";
            if (sectionDifficulty > 0.7)
                return "صعوبة عالية في أسئلة هذا المحور";
            if (avgTimePerQuestion < 10)
                return "حل سريع جدًا، مؤشر على عدم التركيز";
            return "حاجة إلى مراجعة إضافية";
        }

        // 🔹 حساب صعوبة المحور
        private async Task<double> GetSectionDifficultyAsync(int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // لا يوجد DifficultyLevel، نستخدم متوسط ثابت أو مستقبلاً نربطه بمؤشر حقيقي
            var difficulty = await _cache.GetOrCreateAsync($"SectionDifficulty_{sectionId}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(3);
                return await _context.Questions
                    .AsNoTracking()
                    .Where(q => q.Lesson.SectionId == sectionId)
                    .AverageAsync(q => (double?)1) ?? 0.5; // قيمة افتراضية
            });

            return difficulty;
        }

        // 🔹 حساب متوسط الأداء في الواجبات والاختبارات
        private async Task<(double HomeworkAvg, double ExamAvg)> GetStudentPerformanceAveragesAsync(int studentId, int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ✅ متوسط أداء الطالب في الواجبات (من HomeworkSets أو QuestionAttempts)
            var homeworkQuery = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Include(q => q.Question)
                .ThenInclude(q => q.Lesson)
                .ThenInclude(l => l.Section)
                .Where(q => q.StudentId == studentId && q.Question.Lesson.Section.Id == sectionId)
                .ToListAsync();

            double homeworkAvg = 0;
            if (homeworkQuery.Any())
            {
                double total = homeworkQuery.Count();
                double correct = homeworkQuery.Count(q => q.IsCorrect);
                homeworkAvg = total == 0 ? 0 : Math.Round((correct / total) * 100, 2);
            }

            // ✅ متوسط أداء الطالب في الاختبارات (من QuestionAttemptNew المرتبط بالاختبارات)
            var examQuery = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Include(q => q.Question)
                .ThenInclude(q => q.Lesson)
                .ThenInclude(l => l.Section)
                .Where(q => q.StudentId == studentId && q.Question.Lesson.Section.Id == sectionId && q.ExamAssignmentId != null)
                .ToListAsync();

            double examAvg = 0;
            if (examQuery.Any())
            {
                double total = examQuery.Count();
                double correct = examQuery.Count(q => q.IsCorrect);
                examAvg = total == 0 ? 0 : Math.Round((correct / total) * 100, 2);
            }

            return (homeworkAvg, examAvg);
        }

        // 🔹 إنشاء خطة علاجية تلقائيًا
        private async Task CreateRemedialPlanForStudentAsync(int studentId, int sectionId, int examId, string reason)
        {
            using var _context = _contextFactory.CreateDbContext();

            // منع التكرار
            bool exists = await _context.RemedialPlans
         .AsNoTracking()
         .AnyAsync(p =>
             p.StudentID == studentId
             && p.Lessons.Any(l => l.SectionId == sectionId)
             && p.IsCompleted != true);

            if (exists)
                return;

            var section = await _context.Sections
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sectionId);

            var plan = new RemedialPlan
            {
                StudentID = studentId,
                Title = $"خطة علاجية - {section?.Title}",
                Description = $"معالجة الضعف في محور {section?.Title}",
                WeakTopics = section?.Title,
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(7),
                IsAutoGenerated = true,
                TriggerExamId = examId,
                Recommendations = reason,
                PerformanceLevel = "ضعيف",
                CreatedAt = DateTime.Now
            };

            await _context.RemedialPlans.AddAsync(plan);
            await _context.SaveChangesAsync();
            // ✅ إشعار الطالب مباشرة عند توليد الخطة
            await _notificationService.SendToStudentAsync(
      studentId,
      "🎯 خطة علاجية جديدة",
      NotificationCategory.Remedial
  );



            // ✅ تحديث نتيجة الطالب وربطها بالخطة العلاجية
            var result = await _context.StudentIndicatorResults
                .FirstOrDefaultAsync(r =>
                    r.StudentId == studentId &&
                    r.SectionId == sectionId &&
                    r.PerformanceIndicatorExamId == examId);

            if (result != null)
            {
                result.RemedialPlanId = plan.Id;
                result.FailureReason = reason;
                _context.Update(result);
                await _context.SaveChangesAsync();
            }

        }
    }
}
