using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Services.Exams.Implementations
{
    // Sprint 18 (MSE-K / K1): استخراج حرفي لمنطق Areas/Admin/Controllers/MinistrySimExamController.BatchAnalytics
    // (Sprint 15/16، MSE-I) بلا أي تغيير في قواعد الحساب — فقط فصل بناء الـVM الكامل (BuildAnalyticsCoreAsync)
    // عن تحقق وجود الاختبار/الإسناد، ليُعاد استخدام نفس المنطق في GetBatchAverageStatsAsync لتقرير ولي الأمر
    // (ParentReport) بلا تكرار الاستعلام.
    public class MinistrySimExamBatchAnalyticsService : IMinistrySimExamBatchAnalyticsService
    {
        private readonly ApplicationDbContext _context;

        public MinistrySimExamBatchAnalyticsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MinistrySimExamBatchAnalyticsLookup> GetBatchAnalyticsAsync(int ministrySimExamId, int batchId)
        {
            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == ministrySimExamId);

            if (exam == null)
                return new MinistrySimExamBatchAnalyticsLookup { Status = MinistrySimExamBatchAnalyticsStatus.ExamNotFound };

            var assignmentExists = await _context.MinistrySimExamAssignmentsToBatches
                .AsNoTracking()
                .AnyAsync(x => x.MinistrySimExamId == ministrySimExamId && x.BatchId == batchId);

            if (!assignmentExists)
                return new MinistrySimExamBatchAnalyticsLookup { Status = MinistrySimExamBatchAnalyticsStatus.AssignmentNotFound };

            var vm = await BuildAnalyticsCoreAsync(ministrySimExamId, batchId, exam.Title);

            return new MinistrySimExamBatchAnalyticsLookup { Status = MinistrySimExamBatchAnalyticsStatus.Ok, Vm = vm };
        }

        // Sprint 18 (MSE-K / K1): نسخة خفيفة لتقرير ولي الأمر — لا تتحقق من وجود إسناد صريح لهذه الدفعة
        // (الطالب قد يكون مُسنَدًا له الاختبار فرديًا، ودفعته هنا تُستخدم فقط كمرجع مقارنة)، وتُرجع المتوسطات
        // فقط بلا تفصيل قوائم الطلاب/المؤشرات الكاملة.
        public async Task<MinistrySimExamBatchAverageStatsVm> GetBatchAverageStatsAsync(int ministrySimExamId, int batchId)
        {
            var examTitle = await _context.MinistrySimExams
                .AsNoTracking()
                .Where(e => e.Id == ministrySimExamId)
                .Select(e => e.Title)
                .FirstOrDefaultAsync();

            if (examTitle == null)
                return new MinistrySimExamBatchAverageStatsVm();

            var full = await BuildAnalyticsCoreAsync(ministrySimExamId, batchId, examTitle);

            return new MinistrySimExamBatchAverageStatsVm
            {
                TotalStudents = full.TotalStudents,
                AvgScorePercent = full.AvgScorePercent,
                AvgQuantPercent = full.AvgQuantPercent,
                AvgVerbalPercent = full.AvgVerbalPercent
            };
        }

        // استخراج حرفي (بلا أي تغيير في قواعد الحساب) من جسم Controller.BatchAnalytics السابق
        private async Task<MinistrySimExamBatchAnalyticsVm> BuildAnalyticsCoreAsync(int examId, int batchId, string examTitle)
        {
            var batchName = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();

            var studentIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId && e.Status == "Active")
                .Select(e => e.StudentID)
                .ToListAsync();

            var students = await _context.Students
                .AsNoTracking()
                .Where(s => EF.Constant(studentIds).Contains(s.StudentID))
                .OrderBy(s => s.FullName)
                .Select(s => new { s.StudentID, s.FullName })
                .ToListAsync();

            var attempts = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .Where(a => a.MinistrySimExamId == examId && EF.Constant(studentIds).Contains(a.StudentId))
                .Select(a => new { a.Id, a.StudentId, a.IsCompleted, a.TotalScorePercent })
                .ToListAsync();

            var attemptIds = attempts.Select(a => a.Id).ToList();

            var stageQuestions = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .Where(sq => sq.MinistrySimExamStage.MinistrySimExamId == examId)
                .Select(sq => new { sq.Id, sq.IsQuant })
                .ToListAsync();

            var isQuantByStageQuestionId = stageQuestions.ToDictionary(sq => sq.Id, sq => sq.IsQuant);

            var answersQuery = attemptIds.Any()
                ? _context.MinistrySimExamStudentAnswers.AsNoTracking().Where(a => EF.Constant(attemptIds).Contains(a.MinistrySimExamStudentAttemptId))
                : _context.MinistrySimExamStudentAnswers.AsNoTracking().Where(a => false);

            var answers = await answersQuery
                .Select(a => new { a.MinistrySimExamStudentAttemptId, a.MinistrySimExamStageQuestionId, a.IsCorrect, a.SelectedOptionId })
                .ToListAsync();

            var studentSummaries = new List<MinistrySimExamStudentSummaryVm>();
            var quantPercents = new List<double>();
            var verbalPercents = new List<double>();
            var completedCount = 0;
            var inProgressCount = 0;

            foreach (var student in students)
            {
                var attempt = attempts.FirstOrDefault(a => a.StudentId == student.StudentID);
                var summary = new MinistrySimExamStudentSummaryVm
                {
                    StudentId = student.StudentID,
                    FullName = student.FullName,
                    IsCompleted = attempt?.IsCompleted ?? false,
                    ScorePercent = attempt?.TotalScorePercent
                };

                if (attempt != null)
                {
                    if (attempt.IsCompleted) completedCount++;
                    else inProgressCount++;

                    var attemptAnswers = answers.Where(a => a.MinistrySimExamStudentAttemptId == attempt.Id).ToList();

                    var correct = 0;
                    var wrong = 0;
                    var quantCorrect = 0;
                    var quantTotal = 0;
                    var verbalCorrect = 0;
                    var verbalTotal = 0;

                    foreach (var a in attemptAnswers)
                    {
                        if (!isQuantByStageQuestionId.TryGetValue(a.MinistrySimExamStageQuestionId, out bool isQuant))
                            continue;

                        if (isQuant) quantTotal++; else verbalTotal++;

                        if (a.SelectedOptionId == null)
                            continue;

                        if (a.IsCorrect == true)
                        {
                            correct++;
                            if (isQuant) quantCorrect++; else verbalCorrect++;
                        }
                        else
                        {
                            wrong++;
                        }
                    }

                    summary.TotalQuestions = stageQuestions.Count;
                    summary.CorrectAnswers = correct;
                    summary.WrongAnswers = wrong;
                    summary.SkippedAnswers = stageQuestions.Count - correct - wrong;

                    if (attempt.IsCompleted)
                    {
                        if (quantTotal > 0) quantPercents.Add(quantCorrect * 100.0 / quantTotal);
                        if (verbalTotal > 0) verbalPercents.Add(verbalCorrect * 100.0 / verbalTotal);
                    }
                }

                studentSummaries.Add(summary);
            }

            var stageProgressesQuery = attemptIds.Any()
                ? _context.MinistrySimExamStudentStageProgresses.AsNoTracking().Where(sp => EF.Constant(attemptIds).Contains(sp.MinistrySimExamStudentAttemptId))
                : _context.MinistrySimExamStudentStageProgresses.AsNoTracking().Where(sp => false);

            var stageProgresses = await stageProgressesQuery
                .Select(sp => new { sp.StageNumber, sp.IsLocked, sp.TimeExpired, sp.StagePercentScore })
                .ToListAsync();

            var stageStats = stageProgresses
                .GroupBy(sp => sp.StageNumber)
                .Select(g => new MinistrySimExamBatchStageStatVm
                {
                    StageNumber = g.Key,
                    AvgScorePercent = g.Where(sp => sp.IsLocked && sp.StagePercentScore.HasValue)
                        .Select(sp => sp.StagePercentScore.Value)
                        .DefaultIfEmpty(0)
                        .Average(),
                    TimeExpiredCount = g.Count(sp => sp.TimeExpired),
                    CompletedWithinTimeCount = g.Count(sp => sp.IsLocked && !sp.TimeExpired)
                })
                .OrderBy(s => s.StageNumber)
                .ToList();

            return new MinistrySimExamBatchAnalyticsVm
            {
                MinistrySimExamId = examId,
                ExamTitle = examTitle,
                BatchId = batchId,
                BatchName = batchName,
                TotalStudents = students.Count,
                CompletedCount = completedCount,
                InProgressCount = inProgressCount,
                NotStartedCount = students.Count - completedCount - inProgressCount,
                AvgScorePercent = attempts.Where(a => a.IsCompleted).Select(a => a.TotalScorePercent ?? 0).DefaultIfEmpty(0).Average(),
                AvgQuantPercent = quantPercents.DefaultIfEmpty(0).Average(),
                AvgVerbalPercent = verbalPercents.DefaultIfEmpty(0).Average(),
                Students = studentSummaries,
                StageStats = stageStats
            };
        }
    }
}
