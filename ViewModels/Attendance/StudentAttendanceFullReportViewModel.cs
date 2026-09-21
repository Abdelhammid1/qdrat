namespace QdratNew.ViewModels.Attendance
{
    public class StudentAttendanceFullReportViewModel
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string BranchName { get; set; }

        public int TotalLectures { get; set; }
        public int AttendedLectures { get; set; }
        public int AbsentLectures { get; set; }
        public double AttendanceRate { get; set; }
        public string LastLectureAttended { get; set; }

        public List<StudentLectureAttendanceItem> Lectures { get; set; } = new();
    }

    public class StudentLectureAttendanceItem
    {
        public int LectureId { get; set; }
        public string LectureTitle { get; set; }
        public DateTime Date { get; set; }
        public bool IsPresent { get; set; }
        public string? Notes { get; set; }
    }
}
