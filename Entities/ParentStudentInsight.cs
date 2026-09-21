using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class ParentStudentInsight
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Parent")]
        public int ParentId { get; set; }
        public Parent Parent { get; set; } = null!;

        public int StudentId { get; set; }
        public Student Student { get; set; } = null!;

        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string OverallStatus { get; set; } = "مستقر";

        [StringLength(100)]
        public string? CommitmentStatus { get; set; }

        [StringLength(50)]
        public string? LearningTrend { get; set; }

        [StringLength(500)]
        public string? SafeWeaknessSummary { get; set; }

        [StringLength(300)]
        public string? RecommendedAction { get; set; }

        public bool CanRequestSmartPractice { get; set; } = true;

        [StringLength(300)]
        public string? BlockingReason { get; set; }
    }
}
