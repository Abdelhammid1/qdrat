namespace QdratNew.ViewModels.Exam
{
    public class PerformanceExamQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionTitle { get; set; } = "";
        public string StudentAnswer { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";
        public bool IsCorrect { get; set; }
        public string LessonTitle { get; set; } = "";
        public string SectionTitle { get; set; } = "";
        public bool IsQuantitative { get; set; }
        public double TimeTakenSeconds { get; set; }
    }
}
