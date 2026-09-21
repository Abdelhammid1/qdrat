namespace QdratNew.ViewModels.Homework
{
    public class LessonPerformanceVm
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public int TotalQuestions { get; set; }
        public double SuccessRate { get; set; }

     
        public string LessonName { get; set; }
      
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }
        public double Percent { get; set; }
        public double TimeSpentMinutes { get; set; }
    }

}
