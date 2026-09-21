using System;
using System.Collections.Generic;

namespace QdratNew.Entities
{
    // اختبار محاكاة اختبار الوزارة — 5 مراحل ثابتة ومتتالية (كمي + لفظي لكل مرحلة)
    public class MinistrySimExam
    {
        public int Id { get; set; }

        public int CourseId { get; set; }
        public virtual Course Course { get; set; }

        public string Title { get; set; }

        public int TotalStages { get; set; } = 5;
        public int DefaultQuantQuestionsPerStage { get; set; } = 11;
        public int DefaultVerbalQuestionsPerStage { get; set; } = 13;
        public int DefaultDurationMinutesPerStage { get; set; } = 26;

        public bool IsPublished { get; set; } = false;
        public DateTime? PublishedAt { get; set; }

        public int CreatedByInstructorId { get; set; }
        public virtual Instructor CreatedByInstructor { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }

        public virtual ICollection<MinistrySimExamStage> Stages { get; set; } = new List<MinistrySimExamStage>();
        public virtual ICollection<MinistrySimExamAssignmentToBatch> AssignmentsToBatches { get; set; } = new List<MinistrySimExamAssignmentToBatch>();
        public virtual ICollection<MinistrySimExamAssignmentToStudent> AssignmentsToStudents { get; set; } = new List<MinistrySimExamAssignmentToStudent>();
        public virtual ICollection<MinistrySimExamAssignmentToGuest> AssignmentsToGuests { get; set; } = new List<MinistrySimExamAssignmentToGuest>();
        public virtual ICollection<MinistrySimExamStudentAttempt> StudentAttempts { get; set; } = new List<MinistrySimExamStudentAttempt>();
    }
}
