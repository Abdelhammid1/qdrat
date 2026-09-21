namespace QdratNew.ViewModels.Partner.Homework
{
    public class HomeworkDraftPreviewQuestionVM
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public object QuestionType { get; internal set; }
        public bool IsQuantitative { get; internal set; }
    }
}
