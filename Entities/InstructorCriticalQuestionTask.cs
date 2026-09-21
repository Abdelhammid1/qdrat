using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class InstructorCriticalQuestionTask
    {
        public int Id { get; set; }

        [Required]
        public int InstructorId { get; set; }

        [ForeignKey(nameof(InstructorId))]
        public Instructor Instructor { get; set; }

        [Required]
        public int BatchId { get; set; }

        [ForeignKey(nameof(BatchId))]
        public Batch Batch { get; set; }

        [Required]
        public int LessonId { get; set; }

        [ForeignKey(nameof(LessonId))]
        public Lesson Lesson { get; set; }

        public int? CurriculumId { get; set; }

        [ForeignKey(nameof(CurriculumId))]
        public Curriculum Curriculum { get; set; }

        public int? SectionId { get; set; }

        [ForeignKey(nameof(SectionId))]
        public Section Section { get; set; }

        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = "";

        [MaxLength(1000)]
        public string AdminNote { get; set; } = "";

        [MaxLength(1000)]
        public string InstructorNote { get; set; } = "";

        public int CriticalQuestionsCount { get; set; }

        public int AffectedStudentsCount { get; set; }

        public double AverageErrorPercentage { get; set; }

        public bool IsSent { get; set; } = true;

        public bool IsReviewedByInstructor { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public ICollection<InstructorCriticalQuestionTaskItem> Items { get; set; }
            = new List<InstructorCriticalQuestionTaskItem>();
    }
}