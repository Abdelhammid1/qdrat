namespace QdratNew.ViewModels.Exam
{
    public class IndividualExamListVm
    {
        public int AssignmentId { get; set; }
        public int ExamId { get; set; }

        public string ExamTitle { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public DateTime AssignedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsSubmitted { get; set; }

        public int DurationMinutes { get; set; }
        public int TotalQuestions { get; set; }
    }
}
