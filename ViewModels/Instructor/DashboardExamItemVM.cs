namespace QdratNew.ViewModels.Instructor
{
    public class DashboardExamItemVM
    {
        public int ExamAssignmentId { get; set; }
        public string Title { get; set; } = "";
        public string ExamType { get; set; } = "";   // Batch | Performance | Individual
        public string TargetName { get; set; } = "";
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime CreatedAt { get; set; }
        public int SubmittedCount { get; set; }
        public int TotalCount { get; set; }

        public int CompletionPct =>
            TotalCount > 0
                ? (int)Math.Round(SubmittedCount * 100.0 / TotalCount)
                : 0;

        public bool IsLive =>
            StartAt.HasValue &&
            EndAt.HasValue &&
            StartAt.Value <= DateTime.Now &&
            EndAt.Value >= DateTime.Now;
    }
}
