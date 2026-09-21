namespace QdratNew.ViewModels.Instructor
{
    public class InstructorStudentHomeworkInsightViewModel
    {
        public int StudentId { get; set; }

        public string StudentName { get; set; }
        public string BatchName { get; set; }
        public int UnsubmittedCount { get; set; }
    }
}
