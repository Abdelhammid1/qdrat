using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Students;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations
{
    public class StudentExamDashboardService : IStudentExamDashboardService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IStudentExamStatisticsService _statisticsService;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IExamRecommendationService _recommendationService;

        public StudentExamDashboardService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IStudentExamStatisticsService statisticsService,
            ITimeZoneService timeZoneService,
            IExamRecommendationService recommendationService)
        {
            _contextFactory = contextFactory;
            _statisticsService = statisticsService;
            _timeZoneService = timeZoneService;
            _recommendationService = recommendationService;
        }

        // 🟢 لوحة تحكم الطالب (التحليل الكامل)
        public async Task<StudentExamDashboardViewModel> GetDashboardAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();
            var now = _timeZoneService.GetNowSaudi();

            // =============================
            // 1) جلب اختبارات الدفعة للطالب
            // =============================
            var assignedExams = await (
                from sbe in _context.StudentBatchEnrollments
                join batchExam in _context.ExamAssignmentsToBatches on sbe.BatchId equals batchExam.BatchId
                join exam in _context.Exams on batchExam.ExamId equals exam.Id
                where sbe.StudentID == studentId
                      && exam.Type != ExamType.LevelAssessment
                      && exam.Type != ExamType.PerformanceScale
                select new
                {
                    AssignmentId = batchExam.Id,
                    ExamId = exam.Id,
                    Title = batchExam.Title ?? exam.Title,
                    AssignedAt = batchExam.AssignedAt,
                    ScheduledDate = batchExam.ScheduledDate,
                    Duration = batchExam.DurationMinutes
                }
            ).ToListAsync();

            int totalAssigned = assignedExams.Count;

            // لا يوجد أي اختبارات → يرجع نموذج فارغ
            if (!assignedExams.Any())
            {
                return new StudentExamDashboardViewModel
                {
                    TotalAssigned = 0,
                    Completed = 0,
                    Pending = 0,
                    Late = 0,
                    WeaknessCount = 0,
                    MistakesCount = 0,
                    Recommendations = new List<string>
            {
                "⚠️ لا توجد اختبارات مرسلة لك حتى الآن.",
                "سيظهر التحليل هنا فور إرسال أول اختبار."
            },
                    PerformanceAnalysis = new PerformanceAnalysisVm
                    {
                        OverallScore = 0,
                        Strengths = new List<string>(),
                        Weaknesses = new List<string>(),
                        ChartData = new List<ChartDataVm>()
                    }
                };
            }

            // =============================
            // 2) جلب حالة كل اختبار
            // =============================
            var statuses = await _context.ExamStudentStatuses
                .Where(s => s.StudentId == studentId)
                .ToListAsync();

            int completed = 0;
            int pending = 0;
            int late = 0;

            foreach (var exam in assignedExams)
            {
                var status = statuses.FirstOrDefault(s => s.ExamAssignmentId == exam.AssignmentId);
                var start = exam.ScheduledDate ?? exam.AssignedAt;
                var end = start.AddMinutes(exam.Duration);

                if (status != null && status.IsSubmitted)
                {
                    // تم الحل
                    completed++;
                }
                else
                {
                    if (now < start)
                    {
                        // لم يبدأ بعد
                        pending++;
                    }
                    else if (now >= start && now <= end)
                    {
                        // متاح الآن (يعتبر pending)
                        pending++;
                    }
                    else if (now > end)
                    {
                        // انتهى ولم يتم الحل
                        late++;
                    }
                }
            }

            // =============================
            // 3) تحليل الدرجات والأداء
            // =============================
            var realScores = await (
                from s in _context.ExamStudentStatuses
                join e in _context.Exams on s.ExamId equals e.Id
                where s.StudentId == studentId
                      && s.IsSubmitted
                      && s.Score != null
                      && e.Type != ExamType.LevelAssessment
                      && e.Type != ExamType.PerformanceScale
                select (double)s.Score.Value
            ).ToListAsync();

            double overallScore = realScores.Any() ? Math.Round(realScores.Average(), 2) : 0;

            // =============================
            // 4) تحليل المحاور
            // =============================
            var sectionPerformance = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                join ex in _context.Exams on a.ExamId equals ex.Id
                where a.StudentId == studentId
                      && ex.Type != ExamType.LevelAssessment
                      && ex.Type != ExamType.PerformanceScale
                group a by s.Title into g
                select new
                {
                    Section = g.Key,
                    Accuracy = g.Count() > 0 ? (double)g.Count(x => x.IsCorrect) * 100 / g.Count() : 0
                }
            ).ToListAsync();

            var chartData = sectionPerformance
                .Select(x => new ChartDataVm { Label = x.Section, Value = Math.Round(x.Accuracy, 2) })
                .OrderByDescending(x => x.Value)
                .ToList();

            var strengths = sectionPerformance
                .OrderByDescending(x => x.Accuracy)
                .Take(2)
                .Select(x => x.Section)
                .ToList();

            var weaknesses = sectionPerformance
                .Where(x => x.Accuracy < 50)
                .OrderBy(x => x.Accuracy)
                .Select(x => new WeaknessAreaVm
                {
                    SectionName = x.Section,
                    Accuracy = Math.Round(x.Accuracy, 2)
                })
                .ToList();

            // =============================
            // 5) تحليل الأخطاء
            // =============================
            int mistakesCount = await (
                from a in _context.QuestionAttemptNew
                join assign in _context.ExamAssignmentsToBatches on a.ExamAssignmentId equals assign.Id
                join ex in _context.Exams on assign.ExamId equals ex.Id
                where a.StudentId == studentId
                      && a.IsCorrect == false
                      && ex.Type != ExamType.LevelAssessment
                      && ex.Type != ExamType.PerformanceScale
                select a
            ).CountAsync();

            // =============================
            // 6) التوصيات الذكية
            // =============================
            var recommendations = new List<string>();

            if (realScores.Any())
            {
                recommendations.Add($"📊 نتيجتك العامة: {overallScore}%");
                recommendations.Add($"📘 عدد الاختبارات المحلولة: {completed}");
                recommendations.Add($"📗 عدد الاختبارات المتاحة: {pending}");
                recommendations.Add($"📕 الاختبارات المنتهية: {late}");
            }
            else
            {
                recommendations.Add("ابدأ أول اختبار لتفعيل التحليل الذكي لأدائك 💡");
            }

            // =============================
            // 7) بناء النموذج النهائي
            // =============================
            return new StudentExamDashboardViewModel
            {
                TotalAssigned = totalAssigned,
                Completed = completed,
                Pending = pending,
                Late = late,
                WeaknessCount = weaknesses.Count,
                MistakesCount = mistakesCount,
                Recommendations = recommendations,
                WeaknessAreas = weaknesses,
                PerformanceAnalysis = new PerformanceAnalysisVm
                {
                    OverallScore = overallScore,
                    Strengths = strengths,
                    Weaknesses = weaknesses.Select(x => x.SectionName).ToList(),
                    ChartData = chartData
                }
            };
        }

        // 🟢 جلب الاختبارات المكتملة
        public async Task<List<ExamListVm>> GetCompletedExamsAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exams = await (
                from sbe in _context.StudentBatchEnrollments
                join batchExam in _context.ExamAssignmentsToBatches on sbe.BatchId equals batchExam.BatchId
                join exam in _context.Exams on batchExam.ExamId equals exam.Id
                where sbe.StudentID == studentId
                      && (exam.Type != ExamType.LevelAssessment && exam.Type != ExamType.PerformanceScale)
                select new
                {
                    ExamAssignmentId = batchExam.Id,
                    Title = batchExam.Title ?? exam.Title,
                    AssignedAt = batchExam.AssignedAt,
                    IsSubmitted = _context.ExamStudentStatuses
                        .Any(s => s.ExamAssignmentId == batchExam.Id &&
                                  s.StudentId == studentId &&
                                  s.IsSubmitted)
                }
            ).ToListAsync();

            return exams
                .Where(x => x.IsSubmitted)
                .Select(x => new ExamListVm
                {
                    ExamAssignmentId = x.ExamAssignmentId,
                    Title = x.Title ?? "اختبار",
                    AssignedAt = x.AssignedAt,
                    Status = "تم الحل",
                    CanStart = false
                })
                .OrderByDescending(x => x.AssignedAt)
                .ToList();
        }

        // 🟢 جلب الاختبارات المتأخرة
        public async Task<List<ExamListVm>> GetLateExamsAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();
            var now = _timeZoneService.GetNowSaudi();
            var graceHours = 72;

            var exams = await (
                from sbe in _context.StudentBatchEnrollments
                join batchExam in _context.ExamAssignmentsToBatches on sbe.BatchId equals batchExam.BatchId
                join exam in _context.Exams on batchExam.ExamId equals exam.Id
                where sbe.StudentID == studentId
                      && (exam.Type != ExamType.LevelAssessment && exam.Type != ExamType.PerformanceScale)
                select new
                {
                    batchExam.Id,
                    batchExam.Title,
                    batchExam.AssignedAt,
                    batchExam.ScheduledDate,
                    batchExam.DurationMinutes,
                    IsSubmitted = _context.ExamStudentStatuses
                        .Any(s => s.ExamAssignmentId == batchExam.Id &&
                                  s.StudentId == studentId &&
                                  s.IsSubmitted)
                }
            ).ToListAsync();

            var list = new List<ExamListVm>();

            foreach (var ex in exams)
            {
                var start = ex.ScheduledDate ?? ex.AssignedAt;
                var end = start.AddMinutes(ex.DurationMinutes);
                if (ex.IsSubmitted) continue;
                if (now > end && now <= end.AddHours(graceHours))
                    list.Add(new ExamListVm { ExamAssignmentId = ex.Id, Title = ex.Title, AssignedAt = ex.AssignedAt, Status = "📘 متأخر", CanStart = true });
                else if (now > end.AddHours(graceHours))
                    list.Add(new ExamListVm { ExamAssignmentId = ex.Id, Title = ex.Title, AssignedAt = ex.AssignedAt, Status = "📕 متأخر جدًا", CanStart = false });
            }

            return list.OrderByDescending(x => x.AssignedAt).ToList();
        }

        // 🟢 جلب الاختبارات المطلوبة
        public async Task<List<ExamListVm>> GetRequiredExamsAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exams = await (
                from sbe in _context.StudentBatchEnrollments
                join batchExam in _context.ExamAssignmentsToBatches on sbe.BatchId equals batchExam.BatchId
                join exam in _context.Exams on batchExam.ExamId equals exam.Id
                where sbe.StudentID == studentId
                      && (exam.Type != ExamType.LevelAssessment && exam.Type != ExamType.PerformanceScale)
                select new
                {
                    batchExam.Id,
                    batchExam.Title,
                    batchExam.AssignedAt,
                    IsSubmitted = _context.ExamStudentStatuses
                        .Any(s => s.ExamAssignmentId == batchExam.Id &&
                                  s.StudentId == studentId &&
                                  s.IsSubmitted)
                }
            ).ToListAsync();

            return exams
                .Where(x => !x.IsSubmitted)
                .Select(x => new ExamListVm
                {
                    ExamAssignmentId = x.Id,
                    Title = x.Title,
                    AssignedAt = x.AssignedAt,
                    Status = "مطلوب الحل",
                    CanStart = true
                })
                .OrderByDescending(x => x.AssignedAt)
                .ToList();
        }

        // 🟢 المناهج المرتبطة بالطالب
        public async Task<List<SelectListItem>> GetStudentCurriculumsAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var curriculums = await (
                from e in _context.StudentBatchEnrollments
                join b in _context.Batches on e.BatchId equals b.Id
                join cc in _context.CourseCurriculums on b.CourseId equals cc.CourseId
                join c in _context.Curriculums on cc.CurriculumId equals c.Id
                where e.StudentID == studentId
                select new SelectListItem { Value = c.Id.ToString(), Text = c.Title }
            ).Distinct().ToListAsync();

            return curriculums;
        }

        // 🟢 Dashboard حسب المنهج
        public async Task<ExamDashboardChartVm> GetDashboardDataByCurriculumAsync(int studentId, int curriculumId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var sectionPerformance = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                join ex in _context.Exams on a.ExamId equals ex.Id
                where a.StudentId == studentId
                      && s.CurriculumId == curriculumId
                      && ex.Type != ExamType.LevelAssessment
                      && ex.Type != ExamType.PerformanceScale
                group a by s.Title into g
                select new { Section = g.Key, Accuracy = g.Count() > 0 ? (double)g.Count(x => x.IsCorrect) * 100 / g.Count() : 0 }
            ).ToListAsync();

            return new ExamDashboardChartVm
            {
                Labels = sectionPerformance.Select(x => x.Section).ToList(),
                Values = sectionPerformance.Select(x => Math.Round(x.Accuracy, 2)).ToList()
            };
        }

        // 🟢 جميع بيانات الاختبارات الكاملة للطالب
        public async Task<StudentExamFullDataVm> GetExamFullDataAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exams = await (
                from sbe in _context.StudentBatchEnrollments
                join batchExam in _context.ExamAssignmentsToBatches on sbe.BatchId equals batchExam.BatchId
                join exam in _context.Exams on batchExam.ExamId equals exam.Id
                where sbe.StudentID == studentId
                      && (exam.Type != ExamType.LevelAssessment && exam.Type != ExamType.PerformanceScale)
                select new ExamListVm
                {
                    ExamAssignmentId = batchExam.Id,
                    ExamId = exam.Id,
                    Title = batchExam.Title ?? exam.Title,
                    AssignedAt = batchExam.AssignedAt,
                    Status = "تم الحل",
                    CanStart = false
                }
            ).ToListAsync();

            return new StudentExamFullDataVm
            {
                TotalExams = exams.Count,
                CompletedExams = exams.Count,
                Exams = exams
            };
        }
    }
}
