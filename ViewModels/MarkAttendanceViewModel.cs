using QdratNew.Entities;

namespace QdratNew.ViewModels
{
    public class MarkAttendanceViewModel
    {
        public int LectureId { get; set; }
        public string LectureTitle { get; set; }
        public string SectionTitle { get; set; }
        public string CourseName { get; set; }
        public DateTime LectureDate { get; set; }

        public List<StudentAttendanceItem> Students { get; set; } = new();





    }

    public class StudentAttendanceItem
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string BranchName { get; set; }
        public bool IsPresent { get; set; }
    }

}
