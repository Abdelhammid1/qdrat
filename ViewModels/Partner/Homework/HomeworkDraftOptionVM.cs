namespace QdratNew.ViewModels.Partner.Homework
{
    public class HomeworkDraftOptionVM
    {
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public int Id { get; internal set; }
        public string? ImageUrl { get; internal set; }
    }

}
