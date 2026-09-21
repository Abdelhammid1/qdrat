namespace QdratNew.ViewModels.Instructor
{
    public class DashboardHomeworkItemVM
    {
        public int HomeworkSetId { get; set; }
        public string Title { get; set; } = "";
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalStudents { get; set; }
        public int SubmittedCount { get; set; }

        public int CompletionPct =>
            TotalStudents > 0
                ? (int)Math.Round(SubmittedCount * 100.0 / TotalStudents)
                : 0;

        public bool IsActive => EndAt.HasValue && EndAt.Value > DateTime.Now
                              && (!StartAt.HasValue || StartAt.Value <= DateTime.Now);
        public bool IsClosed => EndAt.HasValue && EndAt.Value <= DateTime.Now;
        public bool IsExpired => IsClosed;
    }

}
