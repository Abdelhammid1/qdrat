namespace QdratNew.ViewModels.Exam
{
    public class IndividualExamDetailsViewModel
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public string ReferenceCode { get; set; }
        public int DurationMinutes { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<StudentItem> Students { get; set; } = new();

        public class StudentItem
        {
            public int StudentId { get; set; }
            public string FullName { get; set; }
        }
    }
}
