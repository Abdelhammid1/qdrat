namespace QdratNew.ViewModels.Attendance
{
    public class BatchAttendanceSummaryViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string CourseName { get; set; }
        public int StudentsCount { get; set; }
        public int TotalLectures { get; set; }
        public double AverageAttendanceRate { get; set; }
    }
}
