using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class InstructorCriticalQuestionTaskItem
    {
        public int Id { get; set; }

        [Required]
        public int InstructorCriticalQuestionTaskId { get; set; }

        [ForeignKey(nameof(InstructorCriticalQuestionTaskId))]
        public InstructorCriticalQuestionTask Task { get; set; }

        [Required]
        public Guid QuestionId { get; set; }

        [ForeignKey(nameof(QuestionId))]
        public Question Question { get; set; }

        public int LessonId { get; set; }

        public int? SectionId { get; set; }

        public int? HomeworkSetId { get; set; }

        public int? ExamAssignmentId { get; set; }

        public int? ExamId { get; set; }

        public int? PerformanceIndicatorExamId { get; set; }

        public int TotalAttempts { get; set; }

        public int WrongAttempts { get; set; }

        public int CorrectAttempts { get; set; }

        public int AffectedStudentsCount { get; set; }

        public double ErrorPercentage { get; set; }

        [MaxLength(50)]
        public string SourceType { get; set; } = "";

        [MaxLength(500)]
        public string QuestionTitleSnapshot { get; set; } = "";

        [MaxLength(100)]
        public string ReferenceNumberSnapshot { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}