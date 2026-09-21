namespace QdratNew.ViewModels.Batch
{
    public class InstructorBatchPerformanceViewModel
    {
        public int InstructorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Specialization { get; set; } = "مدرب";
        public string AvatarText { get; set; } = "مد";
        public bool IsActive { get; set; }

        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public int TotalStudents { get; set; }

        // إجماليات
        public int TotalLectures { get; set; }
        public int LecturesThisWeek { get; set; }
        public int TotalHomeworksCreated { get; set; }
        public int TotalExamsCreated { get; set; }
        public double OverallAttendancePercent { get; set; }
        public double OverallHomeworkSubmissionPercent { get; set; }
        public double OverallExamAvgScore { get; set; }
        public double ActivityScore { get; set; }
        public string ActivityCssColor { get; set; } = "var(--blue)";

        // تفاصيل المحاضرات
        public List<InstructorLectureRowVm> Lectures { get; set; } = new();

        // تفاصيل الواجبات
        public List<InstructorHomeworkRowVm> Homeworks { get; set; } = new();

        // تفاصيل الاختبارات
        public List<InstructorExamRowVm> Exams { get; set; } = new();

        // تشخيص المشاكل
        public List<InstructorBatchIssueVm> Issues { get; set; } = new();
    }

    public class InstructorLectureRowVm
    {
        public int LectureId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int PresentCount { get; set; }
        public int TotalStudents { get; set; }
        public double AttendancePercent => TotalStudents > 0
            ? Math.Round((double)PresentCount / TotalStudents * 100, 1) : 0;
        public string AttendanceCssClass => AttendancePercent >= 80 ? "good"
            : AttendancePercent >= 60 ? "warn" : "bad";
        public bool IsLowAttendance => AttendancePercent < 60;
    }

    public class InstructorHomeworkRowVm
    {
        public int HomeworkSetId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? EndAt { get; set; }
        public int SubmittedCount { get; set; }
        public int ExpectedCount { get; set; }
        public double SubmissionPercent => ExpectedCount > 0
            ? Math.Round((double)SubmittedCount / ExpectedCount * 100, 1) : 0;
        public double AverageScore { get; set; }
        public string StatusCssClass => SubmissionPercent >= 80 ? "good"
            : SubmissionPercent >= 60 ? "warn" : "bad";
    }

    public class InstructorExamRowVm
    {
        public int AssignmentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int SubmittedCount { get; set; }
        public int TotalStudents { get; set; }
        public double AverageScore { get; set; }
        public string ScoreCssClass => AverageScore >= 75 ? "good"
            : AverageScore >= 55 ? "warn" : "bad";
    }

    public class InstructorBatchIssueVm
    {
        public string Icon { get; set; } = "⚠";
        public string CssClass { get; set; } = "warn";
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}
