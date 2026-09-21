namespace QdratNew.ViewModels.Batch
{
    public class HomeworkSetDetailViewModel
    {
        public int HomeworkSetId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string InstructorName { get; set; } = "غير محدد";
        public DateTime CreatedAt { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public bool IsSent { get; set; }
        public bool IsClosed { get; set; }
        public bool AllowRetake { get; set; }
        public int MaxRetakes { get; set; }

        public int ExpectedCount { get; set; }
        public int SubmittedCount { get; set; }
        public int MissingCount => ExpectedCount - SubmittedCount;
        public double SubmissionPercent => ExpectedCount > 0
            ? Math.Round((double)SubmittedCount / ExpectedCount * 100, 1) : 0;
        public double AverageScore { get; set; }
        public double MaxScore { get; set; }
        public double MinScore { get; set; }

        public List<HomeworkStudentRowVm> Students { get; set; } = new();
    }

    public class HomeworkStudentRowVm
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string AvatarText { get; set; } = "ط";
        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public double? Score { get; set; }
        public string ScoreCssClass { get; set; } = "neutral";
        public bool IsLate { get; set; }
    }
}
