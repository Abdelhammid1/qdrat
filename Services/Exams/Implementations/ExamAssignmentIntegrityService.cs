using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Exams.Interfaces;

namespace QdratNew.Services.Exams.Implementations
{
    public class ExamAssignmentIntegrityService : IExamAssignmentIntegrityService
    {
        private readonly ApplicationDbContext _context;

        public ExamAssignmentIntegrityService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AssignmentAttemptSummary> GetAttemptSummaryAsync(
            int? examAssignmentId, int? examAssignmentToStudentId)
        {
            if (!examAssignmentId.HasValue && !examAssignmentToStudentId.HasValue)
                return new AssignmentAttemptSummary();

            var attemptsQuery = _context.QuestionAttemptNew.AsNoTracking().AsQueryable();
            var statusQuery = _context.ExamStudentStatuses.AsNoTracking().AsQueryable();

            if (examAssignmentToStudentId.HasValue)
            {
                attemptsQuery = attemptsQuery.Where(a => a.ExamAssignmentToStudentId == examAssignmentToStudentId.Value);
                statusQuery = statusQuery.Where(s => s.ExamAssignmentToStudentId == examAssignmentToStudentId.Value);
            }
            else
            {
                attemptsQuery = attemptsQuery.Where(a => a.ExamAssignmentId == examAssignmentId.Value);
                statusQuery = statusQuery.Where(s => s.ExamAssignmentId == examAssignmentId.Value);
            }

            var attemptedQuestionsCount = await attemptsQuery.Select(a => a.QuestionId).Distinct().CountAsync();
            var affectedStudentsCount = await attemptsQuery.Select(a => a.StudentId).Distinct().CountAsync();
            var isSubmitted = await statusQuery.AnyAsync(s => s.IsSubmitted);

            return new AssignmentAttemptSummary
            {
                AttemptedQuestionsCount = attemptedQuestionsCount,
                IsSubmitted = isSubmitted,
                AffectedStudentsCount = affectedStudentsCount
            };
        }

        public async Task ResetStudentAttemptsAsync(
            int? examAssignmentId, int? examAssignmentToStudentId)
        {
            if (!examAssignmentId.HasValue && !examAssignmentToStudentId.HasValue)
                return;

            // نفس نمط التنظيف الصحيح الموجود بالفعل في ExamIndividualAssignmentsController.Delete
            var attempts = examAssignmentToStudentId.HasValue
                ? _context.QuestionAttemptNew.Where(a => a.ExamAssignmentToStudentId == examAssignmentToStudentId.Value)
                : _context.QuestionAttemptNew.Where(a => a.ExamAssignmentId == examAssignmentId!.Value);

            _context.QuestionAttemptNew.RemoveRange(attempts);

            var statuses = examAssignmentToStudentId.HasValue
                ? _context.ExamStudentStatuses.Where(s => s.ExamAssignmentToStudentId == examAssignmentToStudentId.Value)
                : _context.ExamStudentStatuses.Where(s => s.ExamAssignmentId == examAssignmentId!.Value);

            await foreach (var status in statuses.AsAsyncEnumerable())
            {
                status.Note = null;
                status.IsSubmitted = false;
                status.Status = ExamStatus.Pending;
                status.SubmittedAt = null;
            }

            await _context.SaveChangesAsync();
        }

        public async Task ResetSingleQuestionAttemptAsync(
            Guid questionId, int? examAssignmentId, int? examAssignmentToStudentId)
        {
            if (!examAssignmentId.HasValue && !examAssignmentToStudentId.HasValue)
                return;

            var attempts = examAssignmentToStudentId.HasValue
                ? _context.QuestionAttemptNew.Where(a =>
                    a.QuestionId == questionId && a.ExamAssignmentToStudentId == examAssignmentToStudentId.Value)
                : _context.QuestionAttemptNew.Where(a =>
                    a.QuestionId == questionId && a.ExamAssignmentId == examAssignmentId!.Value);

            _context.QuestionAttemptNew.RemoveRange(attempts);

            var statuses = examAssignmentToStudentId.HasValue
                ? _context.ExamStudentStatuses.Where(s => s.ExamAssignmentToStudentId == examAssignmentToStudentId.Value)
                : _context.ExamStudentStatuses.Where(s => s.ExamAssignmentId == examAssignmentId!.Value);

            // تصفير الـ Snapshot فقط (لا نلمس IsSubmitted/Status) — يجبر إعادة الحساب لهذا السؤال
            // ضمن بقية أسئلة التكليف عند الزيارة التالية للتقرير.
            await foreach (var status in statuses.Where(s => s.IsSubmitted).AsAsyncEnumerable())
            {
                status.Note = null;
            }

            await _context.SaveChangesAsync();
        }
    }
}
