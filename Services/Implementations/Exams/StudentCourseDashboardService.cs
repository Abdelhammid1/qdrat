using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Interfaces.Exams;
using QdratNew.ViewModels.Students;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations.Exams
{
    public class StudentCourseDashboardService : IStudentCourseDashboardService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IStudentExamStatusService _examStatusService;
        private readonly IStudentHomeworkStatusService _homeworkStatusService;

        public StudentCourseDashboardService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IStudentExamStatusService examStatusService,
            IStudentHomeworkStatusService homeworkStatusService)
        {
            _contextFactory = contextFactory;
            _examStatusService = examStatusService;
            _homeworkStatusService = homeworkStatusService;
        }

        public async Task<StudentMainDashboardVm> GetCourseDashboardAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            using var context = _contextFactory.CreateDbContext();

            var vm = new StudentMainDashboardVm();

            // =====================================================
            // 1️⃣ Counters – Homework
            // =====================================================

            var hwSummary = await _homeworkStatusService
                .GetHomeworkSummaryAsync(studentId, courseId, batchId);

            vm.TotalHomeworks = hwSummary.Total;
            vm.CompletedHomeworks = hwSummary.Completed;
            vm.PendingHomeworks = hwSummary.Required;
            vm.LateHomeworks = hwSummary.Late;

            vm.SuccessRateHomeworks =
                vm.TotalHomeworks == 0
                    ? 0
                    : Math.Round((double)vm.CompletedHomeworks / vm.TotalHomeworks * 100, 2);

            vm.HomeworkCompletionRate = vm.SuccessRateHomeworks;

            // =====================================================
            // 2️⃣ Counters – Exams
            // =====================================================

            var examSummary = await _examStatusService
                .GetStatusSummaryAsync(studentId, courseId, batchId);

            vm.TotalExams = examSummary.Total;
            vm.CompletedExams = examSummary.Solved;
            vm.PendingExams = examSummary.Required;
            vm.LateExams = examSummary.Late;

            vm.SuccessRateExams =
                vm.TotalExams == 0
                    ? 0
                    : Math.Round((double)vm.CompletedExams / vm.TotalExams * 100, 2);

            vm.ExamCompletionRate = vm.SuccessRateExams;

            // =====================================================
            // 3️⃣ Homework Progress + Comparison
            // =====================================================

            var homeworkStats = await (
      from a in context.QuestionAttemptNew.AsNoTracking()
      join hs in context.HomeworkSets.AsNoTracking()
          on a.HomeworkSetId equals hs.Id
      where a.HomeworkSetId != null
            && hs.BatchId == batchId
      group a by new { a.HomeworkSetId, a.StudentId } into g
      select new
      {
          g.Key.HomeworkSetId,
          g.Key.StudentId,
          Total = g.Count(),
          Correct = g.Count(x => x.IsCorrect)
      }
  ).ToListAsync();

            var homeworkGrouped = homeworkStats
                .GroupBy(x => x.HomeworkSetId)
                .OrderBy(x => x.Key);

            foreach (var hw in homeworkGrouped)
            {
                var studentRow = hw.FirstOrDefault(x => x.StudentId == studentId);

                if (studentRow == null)
                    continue;

                double studentScore =
                    studentRow.Total == 0
                        ? 0
                        : Math.Round((double)studentRow.Correct / studentRow.Total * 100, 2);

                vm.HomeworkProgress.Add(new StudentHomeworkChartVm
                {
                    Title = $"واجب {hw.Key}",
                    Date = DateTime.UtcNow,
                    Correct = studentRow.Correct,
                    Wrong = studentRow.Total - studentRow.Correct,
                    Skipped = 0,
                    Score = studentScore
                });

                var batchScores = hw
                    .Where(x => x.StudentId != studentId)
                    .Select(x =>
                        x.Total == 0
                            ? 0
                            : (double)x.Correct / x.Total * 100)
                    .ToList();

                double batchAverage =
                    batchScores.Any()
                        ? Math.Round(batchScores.Average(), 2)
                        : 0;

                vm.HomeworkComparison.Add(new StudentBatchComparisonVm
                {
                    Title = $"واجب {hw.Key}",
                    StudentScore = studentScore,
                    BatchAverage = batchAverage,
                    Date = DateTime.UtcNow
                });
            }

            vm.BestHomeworkScore = vm.HomeworkProgress.Any()
                ? vm.HomeworkProgress.Max(x => x.Score)
                : 0;

            // =====================================================
            // 4️⃣ Exam Progress + Comparison
            // =====================================================

            var examStats = await (
             from a in context.QuestionAttemptNew.AsNoTracking()
             join ex in context.Set<ExamAssignmentToBatch>().AsNoTracking()
                 on a.ExamAssignmentId equals ex.Id
             where a.ExamAssignmentId != null
                   && ex.BatchId == batchId
             group a by new { a.ExamAssignmentId, a.StudentId } into g
             select new
             {
                 g.Key.ExamAssignmentId,
                 g.Key.StudentId,
                 Total = g.Count(),
                 Correct = g.Count(x => x.IsCorrect)
             }
         ).ToListAsync();

            var examGrouped = examStats
                .GroupBy(x => x.ExamAssignmentId)
                .OrderBy(x => x.Key);

            foreach (var ex in examGrouped)
            {
                var studentRow = ex.FirstOrDefault(x => x.StudentId == studentId);

                if (studentRow == null)
                    continue;

                double studentScore =
                    studentRow.Total == 0
                        ? 0
                        : Math.Round((double)studentRow.Correct / studentRow.Total * 100, 2);

                vm.ExamProgress.Add(new StudentExamChartVm
                {
                    Title = $"اختبار {ex.Key}",
                    Date = DateTime.UtcNow,
                    Correct = studentRow.Correct,
                    Wrong = studentRow.Total - studentRow.Correct,
                    Skipped = 0,
                    Score = studentScore
                });

                var batchScores = ex
                    .Where(x => x.StudentId != studentId)
                    .Select(x =>
                        x.Total == 0
                            ? 0
                            : (double)x.Correct / x.Total * 100)
                    .ToList();

                double batchAverage =
                    batchScores.Any()
                        ? Math.Round(batchScores.Average(), 2)
                        : 0;

                vm.ExamComparison.Add(new StudentBatchComparisonVm
                {
                    Title = $"اختبار {ex.Key}",
                    StudentScore = studentScore,
                    BatchAverage = batchAverage,
                    Date = DateTime.UtcNow
                });
            }

            vm.BestExamScore = vm.ExamProgress.Any()
                ? vm.ExamProgress.Max(x => x.Score)
                : 0;

            return vm;
        }
    }
}
