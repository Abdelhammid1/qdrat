namespace QdratNew.Entities
{
    /// <summary>
    /// يُسجَّل تلقائياً عندما يكون الفارق بين وقت الـ Client ووقت الـ Server أكبر من 30 ثانية.
    /// لا يوقف الاختبار — للتحليل اللاحق فقط.
    /// </summary>
    public class SuspiciousActivity
    {
        public int Id { get; set; }

        public int StudentId { get; set; }

        /// <summary>ExamAssignmentId أو PerformanceIndicatorExamId حسب نوع الاختبار</summary>
        public int ExamContextId { get; set; }

        public Guid QuestionId { get; set; }

        public double ClientTimeTakenSeconds { get; set; }

        public double ServerTimeTakenSeconds { get; set; }

        /// <summary>"General" | "Placement" | "Performance"</summary>
        public string ExamType { get; set; } = string.Empty;

        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }
}
