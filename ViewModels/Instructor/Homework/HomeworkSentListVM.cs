namespace QdratNew.ViewModels.Instructor.Homework
{
    public class HomeworkSentListVM
    {
        public int HomeworkId { get; set; }

        public string Title { get; set; }

        public string CourseName { get; set; }

        public int TotalQuestions { get; set; }

        public int StudentsSolved { get; set; }

        public int StudentsPending { get; set; }

        public DateTime SentAt { get; set; }
    }
}
