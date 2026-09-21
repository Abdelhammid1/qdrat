namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class DecisionLabHighRiskQuestionViewModel
    {
        public Guid QuestionId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string QuestionTitle { get; set; } = string.Empty;

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
        public string RiskLevel { get; set; } = string.Empty;
    }
}
