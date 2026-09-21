namespace QdratNew.ViewModels.Exam
{
    public class ExamSectionPerformanceVm

    {

        public string SectionTitle { get; set; }
        public string SectionName { get; set; }
        public int TotalQuestions { get; set; }
        public int SectionId { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public double SuccessPercent { get; set; }
        public double AccuracyPercent { get; set; }

      
        public double AvgSuccessRate { get; set; }
        public int QuestionCount { get; set; }
        public int LessonCount { get; set; } // 🆕 عدد المؤشرات داخل المحور
      

    }
}
