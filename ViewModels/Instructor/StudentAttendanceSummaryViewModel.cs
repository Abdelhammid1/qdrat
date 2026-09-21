namespace QdratNew.ViewModels.Instructor
{
    public class StudentAttendanceSummaryViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }
        public int TotalLectures { get; set; }
        public int Attended { get; set; }
        public int Missed { get; set; }
    }
}
