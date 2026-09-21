namespace QdratNew.ViewModels.Exam
{
    public class UnifiedExamReviewVm
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; }
        public string CurriculumTitle { get; set; }
        public string ReferenceCode { get; set; }

        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        public bool IsOnline { get; set; }

        public List<UnifiedSectionVm> Sections { get; set; } = new();
    }

    public class UnifiedSectionVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public List<UnifiedQuestionVm> Questions { get; set; } = new();
    }

    public class UnifiedQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public string LessonTitle { get; set; }
        public int DifficultyLevel { get; set; }
        public string CorrectAnswer { get; set; }
        public int Order { get; set; }
    }
}
