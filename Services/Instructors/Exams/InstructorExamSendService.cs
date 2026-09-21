using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Instructors.Exams.Interfaces;
using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Services.Instructors.Exams
{
    public class InstructorExamSendService : IInstructorExamSendService
    {
        private readonly ApplicationDbContext _context;

        public InstructorExamSendService(ApplicationDbContext context)
        {
            _context = context;
        }

        public void SendDraftToBatches(
            SendExamDraftVM model,
            int instructorId)
        {
            if (model == null || model.BatchIds == null || !model.BatchIds.Any())
                throw new InvalidOperationException("يجب اختيار دفعات");

            var strategy = _context.Database.CreateExecutionStrategy();

            strategy.Execute(() =>
            {
                using var transaction = _context.Database.BeginTransaction();

                // ===============================
                // 1️⃣ جلب الأسئلة مرة واحدة
                // ===============================
                var questionIds = _context.ExamDraftQuestions
                    .Where(q => q.ExamDraftId == model.DraftId)
                    .OrderBy(q => q.Order)
                    .Select(q => q.QuestionId)
                    .ToList();

                if (!questionIds.Any())
                    throw new InvalidOperationException("المسودة فارغة");

                // ===============================
                // 2️⃣ تجهيز Exams
                // ===============================
                var exams = new List<Exam>();

                foreach (var batchId in model.BatchIds)
                {
                    exams.Add(new Exam
                    {
                        Title = model.ExamTitle,
                        CreatedAt = DateTime.Now,
                        IsActive = true
                    });
                }

                _context.BulkInsert(exams);

                // ===============================
                // 3️⃣ تجهيز Assignments
                // ===============================
                var assignments = new List<ExamAssignmentToBatch>();

                for (int i = 0; i < exams.Count; i++)
                {
                    assignments.Add(new ExamAssignmentToBatch
                    {
                        ExamId = exams[i].Id,
                        BatchId = model.BatchIds[i],
                        AssignedAt = DateTime.Now,
                        IsSentToStudents = true,
                        Title = model.ExamTitle,
                        TotalQuestions = questionIds.Count,
                        CreatedByInstructorId = instructorId
                    });
                }

                _context.BulkInsert(assignments);

                // ===============================
                // 4️⃣ تجهيز ExamQuestions
                // ===============================
                var examQuestions = new List<ExamQuestion>();

                foreach (var assignment in assignments)
                {
                    int order = 1;

                    foreach (var qId in questionIds)
                    {
                        examQuestions.Add(new ExamQuestion
                        {
                            ExamId = assignment.ExamId.Value,
                            ExamAssignmentId = assignment.Id,
                            QuestionId = qId,
                            Order = order++
                        });
                    }
                }

                _context.BulkInsert(examQuestions);

                // ===============================
                // 5️⃣ تجهيز Student Status
                // ===============================
                var allStudents = _context.StudentBatchEnrollments
                    .Where(x => model.BatchIds.Any(id => id == x.BatchId))
                    .Select(x => new
                    {
                        x.StudentID,
                        x.BatchId
                    })
                    .ToList();

                var statuses = new List<ExamStudentStatus>();

                foreach (var assignment in assignments)
                {
                    var students = allStudents
                        .Where(s => s.BatchId == assignment.BatchId)
                        .ToList();

                    foreach (var s in students)
                    {
                        statuses.Add(new ExamStudentStatus
                        {
                            StudentId = s.StudentID,
                            ExamId = assignment.ExamId.Value,
                            ExamAssignmentId = assignment.Id,
                            Status = ExamStatus.Pending,
                            AssignedAt = DateTime.Now
                        });
                    }
                }

                _context.BulkInsert(statuses);

                transaction.Commit();
            });
        }
    }
}