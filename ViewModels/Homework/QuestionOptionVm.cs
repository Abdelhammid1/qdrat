namespace QdratNew.ViewModels.Homework
{
    public class QuestionOptionVm
    {
        public string Text { get; set; } = "";
        public string? ImageUrl { get; set; }
        public bool IsCorrect { get; internal set; }
        public bool IsSelectedByStudent { get; internal set; }
    }
}
