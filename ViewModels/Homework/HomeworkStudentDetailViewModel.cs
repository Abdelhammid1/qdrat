namespace QdratNew.ViewModels.Homework
{
    public class HomeworkStudentDetailViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public int QuestionsCount { get; set; }
        public DateTime? SentAt { get; set; }
        public string Status { get; set; }
    }
}
