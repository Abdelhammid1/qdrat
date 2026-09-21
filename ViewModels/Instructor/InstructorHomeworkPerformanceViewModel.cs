namespace QdratNew.ViewModels.Instructor
{
    public class InstructorHomeworkPerformanceViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }
        public int TotalChecked { get; set; }
        public int CorrectCount { get; set; }
        public float Percentage { get; set; }
    }
}
