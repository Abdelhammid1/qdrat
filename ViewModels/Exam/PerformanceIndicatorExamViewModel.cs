namespace QdratNew.ViewModels.Exam
{
    public class PerformanceIndicatorExamViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string CurriculumTitle { get; set; }
        public string BatchName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int DurationMinutes { get; set; }
        public double PassPercent { get; set; }
        public string ReferenceCode { get; set; }
        public bool IsOnline { get; set; }
        public bool IsSent { get; set; }
        public int TotalQuestions { get; set; }
        public int TotalSections { get; set; }
        public List<PerformanceIndicatorExamSectionViewModel> Sections { get; set; } = new();
    }

    public class PerformanceIndicatorExamSectionViewModel
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int QuestionCount { get; set; }
        public List<PerformanceIndicatorExamQuestionDetailsVm> Questions { get; set; } = new();
    }

    public class PerformanceIndicatorExamQuestionDetailsVm
    {
        public Guid QuestionId { get; set; }
        public int Order { get; set; }
        public string ReferenceNumber { get; set; }
        public string Title { get; set; }
        public string CurriculumTitle { get; set; }
        public string SectionTitle { get; set; }
        public string LessonTitle { get; set; }
        public int DifficultyLevel { get; set; }
        public string DifficultyText { get; set; }
        public string CorrectAnswer { get; set; }
    }
}
