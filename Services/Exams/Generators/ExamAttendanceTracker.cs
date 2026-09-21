using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Exams.Generators
{
    public class ExamAttendanceTrackerService : IExamAttendanceTracker
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ExamAttendanceTrackerService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// تسجيل الحضور بمجرد دخول الطالب إلى صفحة الاختبار.
        /// </summary>
        public async Task MarkStudentPresentAsync(int examAssignmentId, int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s => s.StudentId == studentId && s.ExamAssignmentId == examAssignmentId);

            if (status == null)
            {
                var assignment = await _context.ExamAssignmentsToBatches
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a => a.Id == examAssignmentId);

                if (assignment == null)
                    return;

                status = new ExamStudentStatus
                {
                    StudentId = studentId,
                    ExamAssignmentId = examAssignmentId,
                    ExamId = assignment.ExamId!.Value,

                    StartedAt = DateTime.UtcNow,
                    Status = ExamStatus.InProgress,
                    IsSubmitted = false
                };

                _context.ExamStudentStatuses.Add(status);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// إغلاق جميع الاختبارات التي انتهى وقتها تلقائيًا حتى لو لم تُملأ EndAt.
        /// </summary>
        public async Task AutoCompleteExpiredExamsAsync()
        {
            using var _context = _contextFactory.CreateDbContext();
            var now = DateTime.UtcNow;

            // 🟢 جلب كل الحالات الجارية
            var expired = await _context.ExamStudentStatuses
                .Include(s => s.ExamAssignment)
                    .ThenInclude(ea => ea.Exam)
                .Where(s => !s.IsSubmitted && s.Status == ExamStatus.InProgress)
                .ToListAsync();

            foreach (var s in expired)
            {
                // 🕒 حساب مدة الاختبار من Assignment
                var duration = s.ExamAssignment?.DurationMinutes ?? 0;

                if (s.StartedAt.HasValue && duration > 0)
                {
                    var expectedEnd = s.StartedAt.Value.AddMinutes(duration);

                    // ✅ انتهى الوقت
                    if (now >= expectedEnd)
                    {
                        s.IsSubmitted = true;
                        s.Status = ExamStatus.Completed;
                        s.EndAt = expectedEnd;
                        s.SubmittedAt = now;
                    }
                }
            }

            if (expired.Any())
                await _context.SaveChangesAsync();
        }

        /// <summary>
        /// تسجيل أن الطالب أنهى الاختبار يدويًا (حتى لو لم يضغط على زر الإنهاء).
        /// </summary>
        public async Task MarkExamAsCompletedAsync(int examAssignmentId, int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s => s.StudentId == studentId && s.ExamAssignmentId == examAssignmentId);

            if (status == null)
            {
                var assignment = await _context.ExamAssignmentsToBatches
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a => a.Id == examAssignmentId);

                if (assignment == null)
                    return;

                status = new ExamStudentStatus
                {
                    StudentId = studentId,
                    ExamAssignmentId = examAssignmentId,
                    ExamId = assignment.ExamId ?? 0,
                    Status = ExamStatus.Completed,
                    IsSubmitted = true,
                    SubmittedAt = DateTime.UtcNow,
                    EndAt = DateTime.UtcNow
                };
                _context.ExamStudentStatuses.Add(status);
            }
            else
            {
                status.Status = ExamStatus.Completed;
                status.IsSubmitted = true;
                status.SubmittedAt = DateTime.UtcNow;
                status.EndAt = DateTime.UtcNow;
                _context.ExamStudentStatuses.Update(status);
            }

            await _context.SaveChangesAsync();
        }
    }
}
