namespace QdratNew.ViewModels.Reports
{
    public class ExamLessonPerformanceVm
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public double AccuracyPercent { get; set; }
    }
}
