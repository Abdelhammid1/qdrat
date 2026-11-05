using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.ViewModels.PerformanceIndicator;

namespace QdratNew.Services.PerformanceIndicator
{
    // APD-S4.2: نتائج طلاب اختبار مؤشر الأداء، مشتركة بين الأدمن والشريك.
    // ownerPartnerId: null = اختبارات المعهد (الأدمن) بكل طلابها، وقيمة = اختبارات هذه المدرسة وطلاب فروعها فقط.
    public static class PerformanceExamResultsBuilder
    {
        public static async Task<PerformanceIndicatorExamStudentsReportViewModel?> BuildStudentsReportAsync(
            ApplicationDbContext context,
            int examId,
            int? ownerPartnerId,
            CancellationToken ct = default)
        {
            var exam = await context.PerformanceIndicatorExams
                .AsNoTracking()
                .Include(e => e.Curriculum)
                .Include(e => e.ExamStudents.Where(s => ownerPartnerId == null || s.Student.Branch.PartnerId == ownerPartnerId))
                    .ThenInclude(s => s.Student)
                .FirstOrDefaultAsync(e => e.Id == examId && e.OwnerPartnerId == ownerPartnerId, ct);

            if (exam == null)
                return null;

            var passPercent = exam.PassPercent > 0 && exam.PassPercent <= 100
                ? exam.PassPercent
                : 60;

            var examQuestions = await context.PerformanceIndicatorExamQuestions
                .AsNoTracking()
                .Include(eq => eq.Section)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Section)
                .Where(eq => eq.PerformanceIndicatorExamId == examId)
                .OrderBy(eq => eq.OrderNumber)
                .ThenBy(eq => eq.Id)
                .ToListAsync(ct);

            var attempts = await context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.PerformanceIndicatorExamId == examId)
                .ToListAsync(ct);

            var examQuestionIds = examQuestions
                .Select(eq => eq.QuestionId)
                .ToHashSet();

            var sectionGroups = examQuestions
                .Where(eq => eq.Question != null)
                .GroupBy(eq => new
                {
                    SectionId = eq.SectionId
                        ?? eq.Question.Lesson?.SectionId
                        ?? eq.Question.SectionId
                        ?? 0,
                    SectionTitle = eq.Section?.Title
                        ?? eq.Question.Lesson?.Section?.Title
                        ?? eq.Question.Section?.Title
                        ?? "محور غير محدد"
                })
                .ToList();

            var studentRows = new List<PerformanceIndicatorExamStudentRowViewModel>();
            var totalQuestions = examQuestions.Count;

            var blockedStudentIds = await context.IntegrityViolationLogs
                .Where(v => v.AttemptType == IntegrityAttemptType.PerformanceIndicator
                         && v.AttemptEntityId == exam.Id
                         && !v.IsResolved)
                .Select(v => v.StudentId)
                .ToListAsync(ct);
            var blockedStudentIdsSet = blockedStudentIds.ToHashSet();

            foreach (var examStudent in exam.ExamStudents.OrderBy(s => s.Student?.FullName))
            {
                var latestAttempts = attempts
                    .Where(a => a.StudentId == examStudent.StudentId && examQuestionIds.Contains(a.QuestionId))
                    .GroupBy(a => a.QuestionId)
                    .Select(g => g.OrderByDescending(a => a.AttemptedAt).ThenByDescending(a => a.Id).First())
                    .ToList();

                var attemptsByQuestionId = latestAttempts.ToDictionary(a => a.QuestionId, a => a);
                var correctCount = examQuestions.Count(eq =>
                    attemptsByQuestionId.TryGetValue(eq.QuestionId, out var attempt) &&
                    !string.IsNullOrWhiteSpace(attempt.SelectedAnswer) &&
                    attempt.SelectedAnswer != "—" &&
                    attempt.IsCorrect);

                var scorePercent = totalQuestions > 0
                    ? Math.Round(correctCount * 100.0 / totalQuestions, 1)
                    : 0;

                var sectionScores = new List<StudentWeakSectionViewModel>();

                foreach (var sectionGroup in sectionGroups)
                {
                    var sectionTotal = sectionGroup.Count();
                    var sectionCorrect = sectionGroup.Count(eq =>
                        attemptsByQuestionId.TryGetValue(eq.QuestionId, out var attempt) &&
                        !string.IsNullOrWhiteSpace(attempt.SelectedAnswer) &&
                        attempt.SelectedAnswer != "—" &&
                        attempt.IsCorrect);

                    var sectionScore = sectionTotal > 0
                        ? Math.Round(sectionCorrect * 100.0 / sectionTotal, 1)
                        : 0;

                    sectionScores.Add(new StudentWeakSectionViewModel
                    {
                        SectionId = sectionGroup.Key.SectionId,
                        SectionTitle = sectionGroup.Key.SectionTitle,
                        ScorePercent = sectionScore,
                        CorrectCount = sectionCorrect,
                        TotalQuestions = sectionTotal
                    });
                }

                var started = examStudent.StartedAt.HasValue || latestAttempts.Any();
                var statusText = examStudent.IsCompleted
                    ? "مكتمل"
                    : started ? "بدأ ولم يكمل" : "لم يبدأ";

                studentRows.Add(new PerformanceIndicatorExamStudentRowViewModel
                {
                    StudentId = examStudent.StudentId,
                    StudentName = examStudent.Student?.FullName ?? "طالب غير محدد",
                    IsCompleted = examStudent.IsCompleted,
                    StartedAt = examStudent.StartedAt,
                    CompletedAt = examStudent.CompletedAt,
                    ScorePercent = scorePercent,
                    StatusText = statusText,
                    Passed = scorePercent >= passPercent,
                    NeedsRemedialPlan = scorePercent < passPercent,
                    WeakSections = sectionScores.Where(s => s.ScorePercent < passPercent).ToList(),
                    SectionScores = sectionScores,
                    IsIntegrityBlocked = blockedStudentIdsSet.Contains(examStudent.StudentId),
                });
            }

            return new PerformanceIndicatorExamStudentsReportViewModel
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                CurriculumTitle = exam.Curriculum?.Title ?? "—",
                TotalStudents = studentRows.Count,
                CompletedStudents = studentRows.Count(s => s.IsCompleted),
                PassedStudents = studentRows.Count(s => s.Passed),
                NeedRemedialPlanStudents = studentRows.Count(s => s.NeedsRemedialPlan),
                AverageScore = studentRows.Any()
                    ? Math.Round(studentRows.Average(s => s.ScorePercent ?? 0), 1)
                    : 0,
                PassPercent = passPercent,
                Students = studentRows
            };
        }

        // null = لا يملك النطاق هذا الاختبار. الطالب غير موجود أو خارج فروع المدرسة: null أيضًا.
        public static async Task<StudentPerformanceIndicatorQuestionReportViewModel?> BuildStudentQuestionsAsync(
            ApplicationDbContext context,
            int examId,
            int studentId,
            int? ownerPartnerId,
            CancellationToken ct = default)
        {
            var exam = await context.PerformanceIndicatorExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId && e.OwnerPartnerId == ownerPartnerId, ct);

            if (exam == null)
                return null;

            var student = await context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == studentId && (ownerPartnerId == null || s.Branch.PartnerId == ownerPartnerId), ct);

            if (student == null)
                return null;

            var examQuestions = await context.PerformanceIndicatorExamQuestions
                .AsNoTracking()
                .Include(eq => eq.Section)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Section)
                .Where(eq => eq.PerformanceIndicatorExamId == examId)
                .OrderBy(eq => eq.OrderNumber)
                .ThenBy(eq => eq.Id)
                .ToListAsync(ct);

            var attempts = await context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.PerformanceIndicatorExamId == examId && a.StudentId == studentId)
                .ToListAsync(ct);

            var latestAttempts = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(a => a.AttemptedAt).ThenByDescending(a => a.Id).First())
                .ToDictionary(a => a.QuestionId, a => a);

            var rows = examQuestions
                .Where(eq => eq.Question != null)
                .Select((eq, index) =>
                {
                    latestAttempts.TryGetValue(eq.QuestionId, out var attempt);

                    var isSkipped = attempt == null ||
                                    string.IsNullOrWhiteSpace(attempt.SelectedAnswer) ||
                                    attempt.SelectedAnswer == "—";
                    var isCorrect = !isSkipped && attempt!.IsCorrect;

                    return new StudentPerformanceIndicatorQuestionRowViewModel
                    {
                        QuestionId = eq.QuestionId,
                        OrderNumber = eq.OrderNumber > 0 ? eq.OrderNumber : index + 1,
                        QuestionTitle = eq.Question.Title ?? "بدون نص",
                        IsQuantitative = eq.Question.IsQuantitative,
                        SectionId = eq.SectionId ?? eq.Question.Lesson?.SectionId ?? eq.Question.SectionId,
                        SectionTitle = eq.Section?.Title
                            ?? eq.Question.Lesson?.Section?.Title
                            ?? eq.Question.Section?.Title
                            ?? "محور غير محدد",
                        LessonId = eq.Question.LessonId,
                        LessonTitle = eq.Question.Lesson?.Title ?? "مؤشر غير محدد",
                        StudentAnswer = isSkipped ? "لم يجب" : attempt!.SelectedAnswer,
                        CorrectAnswer = eq.Question.CorrectAnswer ?? "—",
                        IsCorrect = isCorrect,
                        IsSkipped = isSkipped,
                        StatusText = isSkipped ? "متخطى" : (isCorrect ? "صحيح" : "خطأ"),
                        TimeTakenSeconds = isSkipped ? null : attempt!.TimeTakenSeconds
                    };
                })
                .OrderBy(q => q.OrderNumber)
                .ToList();

            var correctCount = rows.Count(q => q.IsCorrect);
            var skippedCount = rows.Count(q => q.IsSkipped);
            var wrongCount = rows.Count(q => !q.IsCorrect);
            var total = rows.Count;

            return new StudentPerformanceIndicatorQuestionReportViewModel
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                StudentId = student.StudentID,
                StudentName = student.FullName,
                TotalQuestions = total,
                CorrectCount = correctCount,
                WrongCount = wrongCount,
                SkippedCount = skippedCount,
                ScorePercent = total > 0 ? Math.Round(correctCount * 100.0 / total, 1) : 0,
                Questions = rows
            };
        }
    }
}
