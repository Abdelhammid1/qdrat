namespace QdratNew.ViewModels.Analytics
{
    public class WeakLessonVM
    {
        public int LessonId { get; set; }
        public string LessonName { get; set; }

        public int TotalAttempts { get; set; }
        public int WrongAnswers { get; set; }

        public double WrongPercentage { get; set; }
    }
}