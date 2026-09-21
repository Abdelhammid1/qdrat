namespace QdratNew.ViewModels
{
    public class ReviewByLessonViewModel
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = "";
        public List<QuestionCheckboxItem> Questions { get; set; } = new();
    }

    public class QuestionCheckboxItem
    {
        public Guid Id { get; set; }
        public string TitlePreview { get; set; } = "";
        public bool IsSelected { get; set; }
    }
}
