namespace QdratNew.ViewModels.Students
{
    public class StudentAttendanceRowVm
    {
        public int LectureId { get; set; }
        public string LectureTitle { get; set; } = "";
        public DateTime LectureDate { get; set; }
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public bool IsPresent { get; set; }
        public DateTime RecordedAt { get; set; }
    }

    public class StudentHomeworkRowVm
    {
        public int HomeworkSetId { get; set; }
        public string Title { get; set; } = "";
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public DateTime AssignedAt { get; set; }
        public DateTime? EndAt { get; set; }
        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public double? Score { get; set; }
        public bool IsClosed { get; set; }
        public string ScoreCssClass => !Score.HasValue ? "neutral"
            : Score.Value >= 75 ? "good" : Score.Value >= 55 ? "warn" : "bad";
    }

    public class StudentExamRowVm
    {
        public int ExamAssignmentId { get; set; }
        public string Title { get; set; } = "";
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int? Score { get; set; }
        public string ScoreCssClass => !Score.HasValue ? "neutral"
            : Score.Value >= 75 ? "good" : Score.Value >= 55 ? "warn" : "bad";
    }

    public class StudentBatchRowVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = "";
        public DateTime EnrolledAt { get; set; }
    }

    public class StudentActivityItemVm
    {
        public string Icon { get; set; } = "📌";
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
        public DateTime Date { get; set; }
        public string CssTag { get; set; } = "";
    }
}
