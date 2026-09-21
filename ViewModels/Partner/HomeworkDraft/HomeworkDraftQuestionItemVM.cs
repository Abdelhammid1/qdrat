namespace QdratNew.ViewModels.Partner.HomeworkDraft
{
    public class HomeworkDraftQuestionItemVM
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int LessonId { get; set; }
        public int SectionId { get; set; }
    }

}
