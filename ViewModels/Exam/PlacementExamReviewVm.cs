using QdratNew.Enums;

namespace QdratNew.ViewModels.Exam
{
    public class PlacementExamReviewVm
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; }
        public List<PlacementExamReviewSectionVm> Sections { get; set; } = new();
        public string? CurriculumTitle { get; internal set; }
        public string? BatchName { get; internal set; }
        public int TotalQuestions { get; internal set; }
        public int DurationMinutes { get; internal set; }
        public DateTime CreatedAt { get; internal set; }
        public bool IsInLab { get; internal set; }
        public string? ReferenceCode { get; internal set; }

        // 🆕 هل مفعّل خيار "لا أعرف الإجابة" لهذا الاختبار
        public bool AllowDontKnowOption { get; set; }
    }

    public class PlacementExamReviewSectionVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public List<PlacementExamReviewQuestionVm> Questions { get; set; } = new();
    }

    public class PlacementExamReviewQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public string ImageUrl { get; set; }
        public DifficultyLevel Difficulty { get; set; }
        public string LessonTitle { get; internal set; }
        public string? CorrectAnswer { get; internal set; }
        public int Order { get; internal set; }
    }

}
