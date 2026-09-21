namespace QdratNew.ViewModels.Reports
{
    public class AdminLessonVm
    {
        public int LessonId { get; set; }

        public string LessonTitle { get; set; }

        public int TotalQuestions { get; set; }

        public int Correct { get; set; }

        public int Wrong { get; set; }

        public int Skipped { get; set; }

        public double AccuracyPercent { get; set; }
    }
}
