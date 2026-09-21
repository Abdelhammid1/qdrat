namespace QdratNew.ViewModels.Students
{
    public class StudentAttendancePageVM
    {
        public List<StudentAttendanceViewModel> Attendance { get; set; } = new();
        public List<BatchMiniVM> Batches { get; set; } = new();
    }

    public class BatchMiniVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
