namespace QdratNew.ViewModels.Homework
{
    public class HomeworkSetDetailsViewModel
    {
        public int HomeworkSetId { get; set; }
        public string Title { get; set; } = "";
        public string BatchName { get; set; } = "";
        public string CurriculumTitle { get; set; } = "";
        public string CompletionTitle { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public bool IsSent { get; set; }
        public bool IsClosed { get; set; }
        public bool IsExtra { get; set; }
        public bool AllowRetake { get; set; }
        public int MaxRetakes { get; set; }
        public int TotalQuestions { get; set; }
        public int TotalSections { get; set; }
        public int TotalLessons { get; set; }
        public int AssignedStudentsCount { get; set; }
        public List<HomeworkQuestionDetailsItemViewModel> Questions { get; set; } = new();
    }

    public class HomeworkQuestionDetailsItemViewModel
    {
        public Guid QuestionId { get; set; }
        public int Order { get; set; }
        public string Title { get; set; } = "";
        public string CurriculumTitle { get; set; } = "";
        public string SectionTitle { get; set; } = "";
        public string LessonTitle { get; set; } = "";
        public int DifficultyLevel { get; set; }
        public string DifficultyText { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";
        public string ReferenceNumber { get; set; } = "";
    }
}
