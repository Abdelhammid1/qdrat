namespace QdratNew.ViewModels.Attendance
{
    public class BatchAttendanceReportViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string CourseName { get; set; }
        public List<BatchAttendanceLectureRow> Lectures { get; set; } = new();
    }

    public class BatchAttendanceLectureRow
    {
        public int LectureId { get; set; }
        public string LectureTitle { get; set; }
        public string InstructorName { get; set; }
        public DateTime Date { get; set; }
        public int PresentCount { get; set; }
        public int TotalStudents { get; set; }
        public double AttendanceRate { get; set; }
    }
}
