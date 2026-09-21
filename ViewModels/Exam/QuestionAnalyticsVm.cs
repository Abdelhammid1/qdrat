namespace QdratNew.ViewModels.Exam
{
    public class QuestionAnalyticsVm
    {
        public string QuestionText { get; set; }
        public string? ImageUrl { get; set; }
        public string? VerbalPassageContent { get; set; }
        public bool IsQuantitative { get; set; }
        public QdratNew.Enums.QuestionDisplayType DisplayType { get; set; }
        public string StudentAnswer { get; set; }

        public List<QuestionOptionDisplayVm> Options { get; set; } = new();

        public double AvgTimeSeconds { get; set; }
        public double SuccessRate { get; set; }
        public double DifficultyIndex { get; set; } // 100 - SuccessRate

        public string CorrectAnswer { get; set; } = string.Empty;
        public Dictionary<string, double> OptionSuccessRates { get; set; } = new();
    }
}
