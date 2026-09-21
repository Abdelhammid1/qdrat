namespace QdratNew.ViewModels.Exam
{
    public class LessonQuestionsDetailViewModel
    {
        public int AssignmentId { get; set; }
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }

        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }

        public List<StudentQuestionVm> Questions { get; set; } = new();
    }

    public class StudentQuestionVm
    {
        public string QuestionTitle { get; set; }
        public string? StudentAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }
    }
}
