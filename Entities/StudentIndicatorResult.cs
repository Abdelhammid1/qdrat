using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentIndicatorResult
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        [ForeignKey("PerformanceIndicatorExam")]
        public int PerformanceIndicatorExamId { get; set; }
        public PerformanceIndicatorExam PerformanceIndicatorExam { get; set; }

        [ForeignKey("Section")]
        public int SectionId { get; set; }
        public Section Section { get; set; }

        public double ScorePercent { get; set; }
        public bool IsPassed => ScorePercent >= 60;

        public DateTime TakenAt { get; set; } = DateTime.Now;

        public double? AverageTimePerQuestion { get; set; }
        public bool WasRushed { get; set; }
        public bool WasUnfocused { get; set; }
        public string? FailureReason { get; set; }

        public int? RemedialPlanId { get; set; }
        public RemedialPlan? RemedialPlan { get; set; }
        public int CorrectCount { get; internal set; }
        public int WrongCount { get; internal set; }
        public int SkippedCount { get; internal set; }
        public string? Guidance { get; set; }
    }
}
