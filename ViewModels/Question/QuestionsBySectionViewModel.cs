namespace QdratNew.ViewModels.Question
{
    public class QuestionsBySectionViewModel
    {
        public string SectionTitle { get; set; }
        public int SectionId { get; set; }

        public int PendingReviewCount { get; set; }

        public List<QuestionSimpleViewModel> Questions { get; set; } = new();
    }
}
