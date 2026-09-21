namespace QdratNew.ViewModels.Homework
{
    public class LessonConfirmViewModel
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public int SectionId { get; set; }
        public int TotalQuestions { get; set; }
        public int ReviewedQuestions { get; set; }
        public int QuestionsToUse { get; set; }

        // ✅ جديد
        public List<QuestionSummaryViewModel> SelectedQuestions { get; set; } = new();
    }
    public class QuestionSummaryViewModel
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public string Difficulty { get; set; }
        public bool IsReviewed { get; set; }


    
    }
}
