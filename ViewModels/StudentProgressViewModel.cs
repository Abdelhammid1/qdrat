using QdratNew.ViewModels.Students;

namespace QdratNew.ViewModels
{
    public class StudentProgressViewModel
    {
        public int StudentID { get; set; }
        public string FullName { get; set; }
        public string School { get; set; }
        public string Level { get; set; }
        public string Gender { get; set; }

        public List<PerformanceItem> PerformanceRecords { get; set; } = new();
        public List<LectureAttendanceStatus> AttendanceRecords { get; internal set; }
    }

    public class PerformanceItem
    {
        public string CurriculumTitle { get; set; }
        public string SectionTitle { get; set; }
        public double Score { get; set; }
        public DateTime Date { get; set; }
    }





}
