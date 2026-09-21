using QdratNew.Enums;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class ExamStudentStatus
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }

        public int ExamId { get; set; }
        public Exam Exam { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.Now;
        public DateTime? SubmittedAt { get; set; }

        public bool IsSubmitted { get; set; } = false; // ✅ الصحيح

        public int? Score { get; set; } // في حال تم التصحيح

        public string? Note { get; set; } // ملاحظات المعلم أو النظام


       
        public int? ExamAssignmentId { get; set; } // ✅ لازم تضيف دي
        public ExamStatus Status { get; set; } = ExamStatus.Pending; // ✅ لازم Enum ExamStatus

        // ✅ Navigation Property (لو عندك)
        public ExamAssignmentToBatch? ExamAssignment { get; set; }

        // ✅ إضافات جديدة لإدارة الوقت الفعلي
        public DateTime? StartedAt { get; set; }
        public DateTime? EndAt { get; set; }

        public int ResendCount { get; set; } = 0;


        public int? ExamAssignmentToStudentId { get; set; }

        [ForeignKey(nameof(ExamAssignmentToStudentId))]
        public ExamAssignmentToStudent? StudentAssignment { get; set; }
        public int ReviewSeconds { get; internal set; }

        public string? ReviewBehaviorJson { get; set; }

        public int AttemptCount { get; set; } = 1;
    }
}
