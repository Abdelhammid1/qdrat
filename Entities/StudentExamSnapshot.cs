using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class StudentExamSnapshot
    {
        [Key]
        public int Id { get; set; }

        // ======================
        // Scope (إلزامي)
        // ======================
        public int StudentId { get; set; }
        public int CourseId { get; set; }
        public int BatchId { get; set; }

        // ======================
        // Snapshot Type
        // ======================
        // Dashboard / Exam / Placement / Homework / PerformanceScale
        [MaxLength(50)]
        public string SnapshotType { get; set; } = "Dashboard";

        // ======================
        // Exam Scope (اختياري)
        // ======================
        public int? ExamAssignmentId { get; set; }

        // ======================
        // Core Aggregates (للكروت)
        // ======================
        public int TotalExams { get; set; }
        public int CompletedExams { get; set; }
        public int PendingExams { get; set; }
        public int LateExams { get; set; }

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedQuestions { get; set; }

        // ======================
        // Derived Metrics
        // ======================
        public double AccuracyPercent { get; set; }
        public double AverageSolveMinutes { get; set; }

        // ======================
        // Weakness & Mistakes
        // ======================
        public int WeakSectionsCount { get; set; }
        public int MistakesCount { get; set; }

        // ======================
        // JSON Blocks (Charts)
        // ======================
        // Radar Chart
        public string SectionPerformanceJson { get; set; } = "{}";

        // Exam Progress + Comparison
        public string ExamProgressJson { get; set; } = "{}";

        // Optional Recommendations
        public string? RecommendationsJson { get; set; }

        // ======================
        // Versioning
        // ======================
        public int SnapshotVersion { get; set; } = 1;

        // ======================
        // Meta
        // ======================
        public DateTime SnapshotCreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SnapshotUpdatedAt { get; set; }



        public int SkippedAnswers { get; set; }


        // === Charts ===
        public string WrongQuestionsTrendJson { get; set; }  // Line chart

        public bool IsComplete { get; set; } = false;

        public DateTime BuiltAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }





    }
}
