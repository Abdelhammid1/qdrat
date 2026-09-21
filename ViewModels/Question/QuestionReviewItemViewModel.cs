namespace QdratNew.ViewModels.Question
{
    public class QuestionReviewItemViewModel
    {
        public Guid Id { get; set; }
        public string TitlePreview { get; set; }
        public bool IsChecked { get; set; }
        public string? CorrectAnswer { get; set; }

    }
}
