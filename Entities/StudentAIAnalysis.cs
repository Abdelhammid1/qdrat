using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentAIAnalysis
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        public float SuccessRate { get; set; }
        public float SessionCompletionRatio { get; set; }
        public float AverageHomeworkTime { get; set; }
        public int TotalAssignments { get; set; }
        public int TotalExams { get; set; }

        public float RiskScore { get; set; } // قيمة من ML.NET
        public string? AssessedLevel { get; set; }

        public string? RecommendationsJson { get; set; } // JSON يتضمن التوصيات النصية
        public DateTime AnalyzedAt { get; set; }
    }
}
