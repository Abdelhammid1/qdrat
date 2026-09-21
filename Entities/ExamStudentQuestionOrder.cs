using System;

namespace QdratNew.Entities
{
    public class ExamStudentQuestionOrder
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        public int? ExamAssignmentId { get; set; }
        public ExamAssignmentToBatch? ExamAssignment { get; set; }

        public int? ExamAssignmentToStudentId { get; set; }
        public ExamAssignmentToStudent? ExamAssignmentToStudent { get; set; }

        public int? ExamId { get; set; }
        public Exam? Exam { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; } = null!;

        public int OrderNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
