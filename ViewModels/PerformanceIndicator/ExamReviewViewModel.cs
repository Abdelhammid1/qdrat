namespace QdratNew.ViewModels.PerformanceIndicator
{
    public class ExamReviewViewModel
    {
        public int ExamAssignmentId { get; set; }
        public string ExamTitle { get; set; }

        // 🔹 ملخص النتائج
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedQuestions { get; set; }
        public double ScorePercentage { get; set; }

        // 🔹 الأسئلة
        public List<ExamQuestionReviewVm> Questions { get; set; } = new();
        public double AverageTimePerQuestion { get; internal set; }
        public string TimeSpentFormatted { get; internal set; }
    }

    public class ExamQuestionReviewVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string? SelectedAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public List<QuestionOptionVm> Options { get; set; } = new();
    }

    public class QuestionOptionVm
    {
        public string? Text { get; set; }
        public string? ImageUrl { get; set; }
    }

}
