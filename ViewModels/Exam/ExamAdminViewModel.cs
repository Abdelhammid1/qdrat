namespace QdratNew.ViewModels.Exam
{
    public class ExamAdminViewModel
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; }
        public string BatchName { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public bool IsOnline { get; set; }
        public int TotalStudents { get; set; }
        public int SubmittedCount { get; set; }
        public int PendingCount { get; set; }
    }
}
