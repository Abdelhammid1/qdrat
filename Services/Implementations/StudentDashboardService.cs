using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Students;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations
{
    public class StudentDashboardService : IStudentDashboardService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public StudentDashboardService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<StudentMainDashboardVm> GetDashboardDataAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ================================
            // 1) جلب جميع سجلات الأداء
            // ================================
            var performances = await _context.StudentPerformances
                .Where(p => p.StudentID == studentId)
                .AsNoTracking()
                .ToListAsync();

            var exams = performances
                .Where(p =>
                    p.ActivityType == PerformanceActivityType.PlacementExam ||
                    p.ActivityType == PerformanceActivityType.PerformanceIndicatorExam ||
                    p.ActivityType == PerformanceActivityType.FinalExam)
                .ToList();

            var homeworks = performances
                .Where(p => p.ActivityType == PerformanceActivityType.Homework)
                .ToList();

            // ================================
            // 2) إحصاءات عامة
            // ================================
            int totalExams = exams.Count;
            int completedExams = exams.Count(e => e.Score > 0);

            int totalHomeworks = homeworks.Count;
            int completedHomeworks = homeworks.Count(h => h.Score > 0);

            double examCompletionRate =
                totalExams > 0 ? Math.Round((double)completedExams / totalExams * 100, 2) : 0;

            double homeworkCompletionRate =
                totalHomeworks > 0 ? Math.Round((double)completedHomeworks / totalHomeworks * 100, 2) : 0;

            double successRateExams =
                totalExams > 0 ? Math.Round((double)exams.Count(e => e.Score >= 50) * 100 / totalExams, 2) : 0;

            double successRateHomeworks =
                totalHomeworks > 0 ? Math.Round((double)homeworks.Count(h => h.Score >= 50) * 100 / totalHomeworks, 2) : 0;

            // ================================
            // 3) أضعف محور
            // ================================
            var weakest = performances
                .Where(p => p.SectionId != null)
                .GroupBy(p => p.SectionId)
                .Select(g => new
                {
                    SectionId = g.Key,
                    AvgScore = g.Average(x => x.Score)
                })
                .OrderBy(x => x.AvgScore)
                .FirstOrDefault();

            string weakestSectionTitle = "لا يوجد بيانات";
            double weakestSectionScore = 0;

            if (weakest != null)
            {
                weakestSectionScore = Math.Round(weakest.AvgScore, 2);

                weakestSectionTitle = await _context.Sections
                    .Where(s => s.Id == weakest.SectionId)
                    .Select(s => s.Title)
                    .FirstOrDefaultAsync() ?? "غير معروف";
            }

            // ================================
            // 4) التقدم العام
            // ================================
            double avgScore = performances.Any()
                ? performances.Average(x => x.Score)
                : 0;

            double overallProgress = Math.Round(
                (successRateExams * 0.4) +
                (successRateHomeworks * 0.4) +
                (avgScore * 0.2),
                2
            );

            // ================================
            // 5) تحليل الواجبات
            // ================================
            var studentHomeworksSets = await (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                join hs in _context.HomeworkSets.AsNoTracking()
                    on hss.HomeworkSetId equals hs.Id
                where hss.StudentId == studentId
                select new
                {
                    hss.HomeworkSetId,
                    hs.Title,
                    hs.CreatedAt
                }
            ).ToListAsync();

            var homeworkProgress = new List<StudentHomeworkChartVm>();

            foreach (var set in studentHomeworksSets)
            {
                var questionIds = await _context.Homeworks
                    .Where(h => h.HomeworkSetId == set.HomeworkSetId)
                    .Select(h => h.QuestionId)
                    .Distinct()
                    .ToListAsync();

                var attempts = await _context.QuestionAttemptNew
                    .Where(a => a.StudentId == studentId && a.HomeworkSetId == set.HomeworkSetId)
                    .ToListAsync();

                if (!attempts.Any())
                    continue;

                int correct = 0, wrong = 0, skipped = 0;

                foreach (var qid in questionIds)
                {
                    var att = attempts.FirstOrDefault(a => a.QuestionId == qid);

                    if (att == null)
                    {
                        skipped++;
                        continue;
                    }

                    if (att.IsCorrect) correct++;
                    else wrong++;
                }

                int total = questionIds.Count;
                double score = total > 0 ? Math.Round((double)correct / total * 100, 2) : 0;

                homeworkProgress.Add(new StudentHomeworkChartVm
                {
                    Title = set.Title,
                    Date = set.CreatedAt,
                    Score = score,
                    Correct = correct,
                    Wrong = wrong,
                    Skipped = skipped
                });
            }

            double bestHomeworkScore = homeworkProgress.Any()
                ? Math.Round(homeworkProgress.Max(h => h.Score), 2)
                : 0;

            // ================================
            // 6) تحليل الاختبارات (✔ الإصلاح هنا)
            // ================================
            var examGroups = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.ExamAssignmentId != null)
                .GroupBy(a => a.ExamAssignmentId)
                .Select(g => new
                {
                    ExamAssignmentId = g.Key.Value,
                    Correct = g.Count(x => x.IsCorrect),
                    Wrong = g.Count(x => !x.IsCorrect && x.SelectedAnswer != ""),
                    Answered = g.Count(x => x.SelectedAnswer != "")
                })
                .ToListAsync();

            var examProgress = new List<StudentExamChartVm>();

            foreach (var g in examGroups)
            {
                int total = await _context.ExamQuestions
                    .CountAsync(eq => eq.ExamAssignmentId == g.ExamAssignmentId);

                int skipped = Math.Max(0, total - g.Answered);

                var title = await _context.ExamAssignmentsToBatches
                    .Where(e => e.Id == g.ExamAssignmentId)
                    .Select(e => e.Title)
                    .FirstOrDefaultAsync() ?? ("اختبار #" + g.ExamAssignmentId);

                double score = total > 0
                    ? Math.Round((double)g.Correct / total * 100, 2)
                    : 0;

                examProgress.Add(new StudentExamChartVm
                {
                    Title = title,
                    Date = DateTime.UtcNow,
                    Score = score,
                    Correct = g.Correct,
                    Wrong = g.Wrong,
                    Skipped = skipped
                });
            }

            double bestExamScore = examProgress.Any()
                ? Math.Round(examProgress.Max(e => e.Score), 2)
                : 0;

            // ================================
            // 7) مقارنة الطالب مع الدفعة
            // ================================
            int? batchId = await _context.StudentBatchEnrollments
                .Where(e => e.StudentID == studentId && e.Status == "Active")
                .Select(e => (int?)e.BatchId)
                .FirstOrDefaultAsync();

            var batchPerformances = await (
                from perf in _context.StudentPerformances
                join enroll in _context.StudentBatchEnrollments
                    on perf.StudentID equals enroll.StudentID
                where enroll.BatchId == batchId && enroll.Status == "Active"
                select perf
            ).AsNoTracking().ToListAsync();

            var homeworkComparison = homeworkProgress
                .Select(h => new StudentBatchComparisonVm
                {
                    Title = h.Title,
                    StudentScore = h.Score,
                    BatchAverage = Math.Round(
                        batchPerformances
                            .Where(bp => bp.ActivityType == PerformanceActivityType.Homework)
                            .Average(bp => (double?)bp.Score) ?? 0, 2)
                })
                .ToList();

            var examComparison = examProgress
                .Select(e => new StudentBatchComparisonVm
                {
                    Title = e.Title,
                    StudentScore = e.Score,
                    BatchAverage = Math.Round(
                        batchPerformances
                            .Where(bp =>
                                bp.ActivityType == PerformanceActivityType.FinalExam ||
                                bp.ActivityType == PerformanceActivityType.PlacementExam ||
                                bp.ActivityType == PerformanceActivityType.PerformanceIndicatorExam)
                            .Average(bp => (double?)bp.Score) ?? 0, 2)
                })
                .ToList();

            // ================================
            // 8) الإرجاع
            // ================================
            return new StudentMainDashboardVm
            {
                TotalExams = totalExams,
                CompletedExams = completedExams,
                TotalHomeworks = totalHomeworks,
                CompletedHomeworks = completedHomeworks,
                ExamCompletionRate = examCompletionRate,
                HomeworkCompletionRate = homeworkCompletionRate,
                BestExamScore = bestExamScore,
                BestHomeworkScore = bestHomeworkScore,
                SuccessRateExams = successRateExams,
                SuccessRateHomeworks = successRateHomeworks,
                HomeworkProgress = homeworkProgress,
                ExamProgress = examProgress,
                HomeworkComparison = homeworkComparison,
                ExamComparison = examComparison,
                WeakestSectionTitle = weakestSectionTitle,
                WeakestSectionScore = weakestSectionScore,
                OverallProgress = overallProgress
            };
        }
    }
}
