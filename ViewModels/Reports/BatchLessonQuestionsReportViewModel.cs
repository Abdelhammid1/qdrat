using QdratNew.Enums;

namespace QdratNew.ViewModels.Reports
{
    public class BatchLessonQuestionsReportViewModel
    {
        public string ExamTitle { get; set; }
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }

        public int TotalQuestions { get; set; }
        public int TotalCorrect { get; set; }
        public int TotalWrong { get; set; }
        public double OverallPercent { get; set; }

        public List<BatchLessonQuestionEntry> Questions { get; set; } = new();
    }

    public class BatchLessonQuestionEntry
    {
        public Guid QuestionId { get; set; }
        public string? QuestionTitle { get; set; }
        public string? ImageUrl { get; set; }
        public QuestionTemplate Template { get; set; }
        public string? ValueA { get; set; }
        public string? ValueB { get; set; }

        // Verbal Passage
        public bool HasVerbalPassage { get; set; }
        public string? VerbalPassageTitle { get; set; }
        public string? VerbalPassageContent { get; set; }
        public PassageType VerbalPassageType { get; set; }
        public string? VerbalPassageMediaUrl { get; set; }

        // Aggregated stats
        public int TotalAnswers { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedCount { get; set; }
        public double Percent { get; set; }
        public double AvgTimeTakenSeconds { get; set; }

        // Options
        public List<BatchQuestionOptionEntry> Options { get; set; } = new();
    }

    public class BatchQuestionOptionEntry
    {
        public string Label { get; set; } = "";
        public string? Text { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsCorrect { get; set; }
        public int SelectedCount { get; set; }
        public double SelectionPercent { get; set; }
    }
}
