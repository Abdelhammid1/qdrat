using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Services.HomeworkEngine.Instructor
{
    public class InstructorHomeworkEngineService : IInstructorHomeworkEngineService
    {
        private readonly ApplicationDbContext _context;

        public InstructorHomeworkEngineService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> CreateHomeworkForBatchAsync(
            int batchId,
            string title,
            List<Guid> questionIds,
            DateTime? startAt,
            DateTime? endAt,
            string instructorUserId)
        {
            if (questionIds == null || !questionIds.Any())
                throw new InvalidOperationException("لا توجد أسئلة محددة.");

            var batch = await _context.Batches
                .FirstOrDefaultAsync(x => x.Id == batchId);

            if (batch == null)
                throw new InvalidOperationException("الدفعة غير موجودة.");

            var homeworkSet = new HomeworkSet
            {
                Title = title,
                CompletionTitle = title,
                BatchId = batchId,
                AssignedByUserId = instructorUserId,
                CreatedAt = DateTime.UtcNow,
                IsSent = true,
                StartAt = startAt,
                EndAt = endAt
            };

            _context.HomeworkSets.Add(homeworkSet);
            await _context.SaveChangesAsync();

            var students = await _context.StudentBatchEnrollments
                .Where(x => x.BatchId == batchId)
                .Select(x => x.StudentID)
                .ToListAsync();

            if (!students.Any())
                return 0;

            var questions = await _context.Questions
                .Where(q => questionIds.Contains(q.Id))
                .Select(q => new
                {
                    q.Id,
                    q.LessonId
                })
                .ToListAsync();

            var homeworkSetStudents = new List<HomeworkSetStudent>();
            var homeworks = new List<QdratNew.Entities.Homework>();

            foreach (var studentId in students)
            {
                homeworkSetStudents.Add(new HomeworkSetStudent
                {
                    HomeworkSetId = homeworkSet.Id,
                    StudentId = studentId,
                    AssignedAt = DateTime.UtcNow,
                    IsSubmitted = false
                });

                foreach (var q in questions)
                {
                    homeworks.Add(new QdratNew.Entities.Homework
                    {
                        HomeworkSetId = homeworkSet.Id,
                        StudentId = studentId,
                        QuestionId = q.Id,
                        LessonId = q.LessonId,
                        AssignedAt = DateTime.UtcNow,
                        Status = HomeworkStatus.Pending,
                        IsSent = true
                    });
                }
            }

            await _context.BulkInsertAsync(homeworkSetStudents);
            await _context.BulkInsertAsync(homeworks);

            return homeworks.Count;
        }
    }
}