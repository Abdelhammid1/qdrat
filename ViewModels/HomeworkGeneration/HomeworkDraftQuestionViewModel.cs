namespace QdratNew.ViewModels.HomeworkGeneration
{
    public class HomeworkDraftQuestionViewModel
    {
        public int QuestionId { get; set; }

        public string QuestionText { get; set; } = string.Empty;

        public string QuestionType { get; set; } = string.Empty;

        public bool IsQuantitative { get; set; }
    }
}
