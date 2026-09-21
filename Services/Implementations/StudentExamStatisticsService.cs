using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Implementations
{
    public class StudentExamStatisticsService : IStudentExamStatisticsService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ITimeZoneService _timeZoneService;

        public StudentExamStatisticsService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ITimeZoneService timeZoneService)
        {
            _contextFactory = contextFactory;
            _timeZoneService = timeZoneService;
        }

        // ============================================================
        // 1) إحصاءات الاختبارات العامة للطالب (Dashboard)
        // ============================================================
        public async Task<ExamStatisticsViewModel> GetExamStatisticsAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();
            var now = _timeZoneService.GetNowSaudi();

            var examsQuery = await (
                from sbe in _context.StudentBatchEnrollments
                join batchExam in _context.ExamAssignmentsToBatches on sbe.BatchId equals batchExam.BatchId
                join exam in _context.Exams on batchExam.ExamId equals exam.Id
                where sbe.StudentID == studentId
                select new
                {
                    ExamId = exam.Id,
                    AssignmentId = batchExam.Id,
                    Title = batchExam.Title ?? exam.Title,
                    AssignedAt = batchExam.AssignedAt,
                    DueDate = batchExam.AssignedAt.AddMinutes(batchExam.DurationMinutes),
                    IsSubmitted = _context.ExamStudentStatuses
                        .Any(s => s.ExamAssignmentId == batchExam.Id &&
                                  s.StudentId == studentId &&
                                  s.IsSubmitted)
                }
            ).ToListAsync();

            return new ExamStatisticsViewModel
            {
                TotalExams = examsQuery.Count,
                CompletedExams = examsQuery.Count(x => x.IsSubmitted),
                PendingExams = examsQuery.Count(x => !x.IsSubmitted && x.DueDate > now),
                LateExams = examsQuery.Count(x => !x.IsSubmitted && now > x.DueDate),
                Exams = examsQuery.Select(x => new ExamItemVm
                {
                    ExamId = x.ExamId,
                    AssignmentId = x.AssignmentId,
                    Title = x.Title,
                    AssignedAt = x.AssignedAt,
                    DueDate = x.DueDate,
                    IsSubmitted = x.IsSubmitted,
                    StatusText = x.IsSubmitted ? "تم الحل"
                                : (x.DueDate <= now ? "متأخر" : "مطلوب")
                }).ToList()
            };
        }


        // ============================================================
        // 2) الإحصاءات التفصيلية (دفعة – فردي – مؤشر أداء – مستوى)
        // ============================================================
        public async Task<ExamResultSummaryVm> GetExamResultAsync(int examIdOrAssignmentId, int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ---------------------------
            // 1) تحديد نوع التعيين
            // ---------------------------
            bool isBatchExam = await _context.ExamAssignmentsToBatches
                .AnyAsync(x => x.Id == examIdOrAssignmentId);

            bool isIndividualExam = await _context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == examIdOrAssignmentId && x.StudentId == studentId);

            bool isOldAssignment = await _context.ExamAssignments
                .AnyAsync(x => x.Id == examIdOrAssignmentId);

            var perfExam = await _context.PerformanceIndicatorExams
     .FirstOrDefaultAsync(x => x.Id == examIdOrAssignmentId);

            bool isPerformanceExam = perfExam != null;


           

            List<Guid> questionIds = new();
            List<QuestionAttemptNew> attempts = new();
            double solveMinutes = 0;

            // ============================================================
            // 2) اختبارات الدفعة Batch Exam
            // ============================================================
            if (isBatchExam)
            {
                questionIds = await _context.ExamQuestions
                    .Where(eq => eq.ExamAssignmentId == examIdOrAssignmentId)
                    .Select(eq => eq.QuestionId)
                    .ToListAsync();

                attempts = await _context.QuestionAttemptNew
                    .Where(a =>
                        a.StudentId == studentId &&
                        a.ExamAssignmentId == examIdOrAssignmentId)
                    .ToListAsync();

                var status = await _context.ExamStudentStatuses
                    .FirstOrDefaultAsync(x =>
                        x.StudentId == studentId &&
                        x.ExamAssignmentId == examIdOrAssignmentId);

                if (status?.StartedAt != null)
                {
                    DateTime end = status.SubmittedAt ?? status.EndAt ?? DateTime.UtcNow;
                    solveMinutes = Math.Round((end - status.StartedAt.Value).TotalMinutes, 1);
                }
            }

            // ============================================================
            // 3) الاختبار الفردي Individual Exam
            // ============================================================
            else if (isIndividualExam)
            {
                var assign = await _context.ExamAssignmentsToStudents
                    .FirstOrDefaultAsync(a => a.Id == examIdOrAssignmentId);

                int examId = assign.ExamId;

                questionIds = await _context.ExamQuestions
                    .Where(eq => eq.ExamAssignmentToStudentId == examIdOrAssignmentId)
                    .Select(eq => eq.QuestionId)
                    .ToListAsync();

                attempts = await _context.QuestionAttemptNew
                    .Where(a =>
                        a.StudentId == studentId &&
                        (a.ExamAssignmentToStudentId == examIdOrAssignmentId ||
                         (a.ExamId == examId && a.ExamAssignmentToStudentId == null)))
                    .ToListAsync();

                // 🔥 هذا هو التصحيح المهم
                var status = await _context.ExamStudentStatuses
                    .FirstOrDefaultAsync(x =>
                        x.StudentId == studentId &&
                        x.ExamAssignmentToStudentId == examIdOrAssignmentId);

                if (status?.StartedAt != null)
                {
                    DateTime end = status.SubmittedAt ?? status.EndAt ?? DateTime.UtcNow;
                    solveMinutes = Math.Round((end - status.StartedAt.Value).TotalMinutes, 1);
                }
            }

            // ============================================================
            // 4) التعيين القديم ExamAssignments
            // ============================================================
            // ============================================================
            // 6) اختبار تحديد المستوى Placement Exam (بناءً على Assignment)
            // ============================================================
            else if (isOldAssignment)
            {
                // جلب التعيين مع نوع الاختبار
                var assignment = await _context.ExamAssignments
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a => a.Id == examIdOrAssignmentId);

                if (assignment?.Exam?.Type == ExamType.LevelAssessment)
                {
                    int examId = assignment.ExamId;

                    // الأسئلة مربوطة بالـ ExamId فقط
                    questionIds = await _context.ExamQuestions
                        .Where(eq => eq.ExamId == examId)
                        .Select(eq => eq.QuestionId)
                        .ToListAsync();

                    attempts = await _context.QuestionAttemptNew
                        .Where(a =>
                            a.StudentId == studentId &&
                            a.ExamAssignmentId == examIdOrAssignmentId)
                        .ToListAsync();

                    var status = await _context.ExamStudentStatuses
                        .FirstOrDefaultAsync(s =>
                            s.StudentId == studentId &&
                            s.ExamAssignmentId == examIdOrAssignmentId);

                    if (status?.StartedAt != null)
                    {
                        DateTime end = status.SubmittedAt ?? status.EndAt ?? DateTime.UtcNow;
                        solveMinutes = Math.Round((end - status.StartedAt.Value).TotalMinutes, 1);
                    }
                }
                else
                {
                    // تعيين قديم غير تحديد مستوى
                    questionIds = await _context.ExamQuestions
                        .Where(eq => eq.ExamAssignmentId == examIdOrAssignmentId)
                        .Select(eq => eq.QuestionId)
                        .ToListAsync();

                    attempts = await _context.QuestionAttemptNew
                        .Where(a =>
                            a.StudentId == studentId &&
                            a.ExamAssignmentId == examIdOrAssignmentId)
                        .ToListAsync();
                }
            }






            // ============================================================
            // 5) اختبار مؤشر أداء Performance Indicator Exam
            // ============================================================
            else if (isPerformanceExam)
            {
                int examId = perfExam.Id;

                questionIds = await _context.PerformanceIndicatorExamQuestions
                    .Where(eq => eq.PerformanceIndicatorExamId == examId)
                    .Select(eq => eq.QuestionId)
                    .ToListAsync();

                attempts = await _context.QuestionAttemptNew
                    .Where(a =>
                        a.StudentId == studentId &&
                        a.PerformanceIndicatorExamId == examId)
                    .ToListAsync();

                solveMinutes = Math.Round(attempts.Sum(a => a.TimeTakenSeconds) / 60.0, 1);
            }

            // ============================================================
            // 6) اختبار تحديد المستوى Placement Exam
            // ============================================================
           



            // ============================================================
            // 7) الحسابات النهائية للكروت
            // ============================================================
            int total = questionIds.Count;

            int correct = attempts.Count(a => a.IsCorrect && questionIds.Contains(a.QuestionId));
            int wrong = attempts.Count(a => !a.IsCorrect && questionIds.Contains(a.QuestionId));
            // 🆕 من ضمن wrong: عدد إجابات "لا أعرف الإجابة" (اختبارات تحديد المستوى المفعّل فيها هذا الخيار)
            int dontKnow = attempts.Count(a => a.IsDontKnowAnswer && questionIds.Contains(a.QuestionId));
            int answered = correct + wrong;
            int skipped = total - answered;

            double percent = total == 0 ? 0 : Math.Round(correct * 100.0 / total, 1);

            double avg = attempts.Any(a => a.TimeTakenSeconds > 0)
                ? Math.Round(attempts.Average(a => a.TimeTakenSeconds), 1)
                : 0;

            return new ExamResultSummaryVm
            {
                TotalQuestions = total,
                CorrectAnswers = correct,
                WrongAnswers = wrong,
                DontKnowAnswers = dontKnow,
                SkippedQuestions = skipped,
                AnsweredQuestions = answered,
                ScorePercentage = percent,
                SolveMinutes = solveMinutes,
                AverageTimePerQuestion = avg
            };
        }
    }
}
