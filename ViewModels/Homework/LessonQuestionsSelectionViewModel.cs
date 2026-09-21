namespace QdratNew.ViewModels.Homework
{
    public class LessonQuestionsSelectionViewModel
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public int BatchId { get; set; }

        public List<QuestionSummaryViewModel> Questions { get; set; } = new();
        public List<int> SelectedQuestionIds { get; set; } = new();
    }
}
