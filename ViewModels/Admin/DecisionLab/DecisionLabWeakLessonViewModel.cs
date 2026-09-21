namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class DecisionLabWeakLessonViewModel
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;

        public int SectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;

        public int TotalAttempts { get; set; }
        public int CorrectAttempts { get; set; }
        public int WrongAttempts { get; set; }
        public int AffectedStudentsCount { get; set; }

        public double ErrorPercentage { get; set; }
        public double AverageTimeTakenSeconds { get; set; }

        public int RelatedQuestionsCount { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
    }
}
