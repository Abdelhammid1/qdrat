namespace QdratNew.ViewModels.Partner.Homework
{
    public class HomeworkDraftQuestionVM
    {
        public Guid QuestionId { get; set; }

        public string QuestionText { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;

        public List<HomeworkDraftOptionVM> Options { get; set; } = new();
        public int LessonId { get; internal set; }
        public string? Title { get; internal set; }
        public bool IsQuantitative { get; internal set; }
        public int SectionId { get; internal set; }
    }

}
