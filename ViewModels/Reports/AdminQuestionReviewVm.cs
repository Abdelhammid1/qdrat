namespace QdratNew.ViewModels.Reports
{
    public class AdminQuestionReviewVm
    {
        public Guid QuestionId { get; set; }

        public string QuestionTitle { get; set; }
        public string VerbalPassageContent { get; set; }
        public string ImageUrl { get; set; }
        public string ComparisonValue1 { get; set; }
        public string ComparisonValue2 { get; set; }
        public QdratNew.Enums.QuestionDisplayType DisplayType { get; set; }
        public bool IsQuantitative { get; set; }

        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }

        public List<AdminQuestionOptionVm> Options { get; set; } = new();
    }

    public class AdminQuestionOptionVm
    {
        public string Text { get; set; }
        public string ImageUrl { get; set; }
    }

}
