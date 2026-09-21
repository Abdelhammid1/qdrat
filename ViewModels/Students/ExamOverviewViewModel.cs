namespace QdratNew.ViewModels.Students
{
    public class ExamOverviewViewModel
    {
        public int ExamAssignmentId { get; set; }
        public string Title { get; set; }
        public string SectionTitle { get; set; }
        public int TotalQuestions { get; set; }
        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedAt { get; set; }



    }
}
