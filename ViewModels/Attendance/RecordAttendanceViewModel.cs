namespace QdratNew.ViewModels.Attendance
{
    public class RecordAttendanceViewModel
    {
        public int LectureId { get; set; }
        public int BatchId { get; set; }

        public List<StudentAttendanceInput> Students { get; set; }
    }

    public class StudentAttendanceInput
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public bool IsPresent { get; set; }
        public string? Notes { get; set; } // لعذر مثل "عذر طبي"

  

    }

}
