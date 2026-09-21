namespace QdratNew.ViewModels.Homework
{
    public class HomeworkOptionReviewItem
    {
        public string? Text { get; set; }        // نص الاختيار
        public string? ImageUrl { get; set; }    // صورة الاختيار (إن وجدت)
        public bool IsCorrect { get; internal set; }
        public bool IsSelectedByStudent { get; internal set; }
    }
}
