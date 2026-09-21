namespace QdratNew.ViewModels.Section
{
    public class SectionAIAnalysisViewModel
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public string CurriculumTitle { get; set; }

        public int TotalQuestions { get; set; }
        public int TotalAttempts { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers => TotalAttempts - CorrectAnswers;

        public double SuccessRate => TotalAttempts > 0 ? (double)CorrectAnswers / TotalAttempts * 100 : 0;
        public double FailureRate => 100 - SuccessRate;
        // ✅ تحليل الصعوبة (مثل: "صعب", "سهل", "متوسط")
        public string DifficultyAnalysis { get; set; }
        public int EasyCount { get; set; }
        public int MediumCount { get; set; }
        public int HardCount { get; set; }
    }


}
