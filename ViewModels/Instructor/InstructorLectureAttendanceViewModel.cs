namespace QdratNew.ViewModels.Instructor
{
    public class InstructorLectureAttendanceViewModel
    {
        public int LectureId { get; set; }
        public string LectureTitle { get; set; }
        public DateTime LectureDate { get; set; }
        public List<InstructorAttendanceStudentViewModel> Students { get; set; }
    }
}
