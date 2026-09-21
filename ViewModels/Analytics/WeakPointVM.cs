namespace QdratNew.ViewModels.Analytics
{
    public class WeakPointVM
    {
        public Guid QuestionId { get; set; }
        public string QuestionTitle { get; set; }

        public int LessonId { get; set; }
        public string LessonName { get; set; }

        public int TotalAttempts { get; set; }
        public int WrongAnswers { get; set; }

        public double WrongPercentage { get; set; }
    }
}