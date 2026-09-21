namespace QdratNew.ViewModels.Reports
{
    public class LessonReviewViewModel
    {
        public int LessonId { get; set; }
        public int AssignmentId { get; set; }
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }

        public List<ReviewQuestionVm> Questions { get; set; } = new();
    }
}
