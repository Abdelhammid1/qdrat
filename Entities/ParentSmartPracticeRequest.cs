using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class ParentSmartPracticeRequest
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Parent")]
        public int ParentId { get; set; }
        public Parent Parent { get; set; } = null!;

        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        public int? CurriculumId { get; set; }
        public Curriculum? Curriculum { get; set; }

        public int? SectionId { get; set; }
        public Section? Section { get; set; }

        [Required]
        public int RequestedQuestionCount { get; set; }

        public int RequestedDurationMinutes { get; set; }

        [Required, StringLength(50)]
        public string PracticeMode { get; set; } = "QuickPractice";

        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? GeneratedExamAssignmentToStudentId { get; set; }
        public ExamAssignmentToStudent? GeneratedExamAssignmentToStudent { get; set; }

        [StringLength(500)]
        public string? SafeSummary { get; set; }

        public string? RecommendationSnapshotJson { get; set; }
    }
}
