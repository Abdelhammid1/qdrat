namespace QdratNew.ViewModels.Exam
{
    public class StudentIndividualExamCardVm
    {
        public int AssignmentId { get; set; }
        public int ExamId { get; set; }
        public string Title { get; set; }
        public DateTime ScheduledDate { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsCompleted { get; set; }
    }

}
