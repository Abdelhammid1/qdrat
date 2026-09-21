namespace QdratNew.ViewModels.Attendance
{
    public class LectureAttendanceReportViewModel
    {
        public int LectureId { get; set; }
        public int BatchId { get; set; }

        public string LectureTitle { get; set; }
        public string InstructorName { get; set; }
        public string CurriculumTitle { get; set; }
        public string BatchName { get; set; }
        public DateTime Date { get; set; }

        public int TotalStudents { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public double AttendanceRate { get; set; }

        public List<LectureAttendanceStudentItem> Students { get; set; } = new();
    }

    public class LectureAttendanceStudentItem
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public bool IsPresent { get; set; }
        public string? Notes { get; set; }
    }
}
