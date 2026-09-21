namespace QdratNew.ViewModels.Question
{
    public class QuestionVm
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string SectionTitle { get; set; }
        public string LessonTitle { get; set; }
        public string InternalNote { get; internal set; }
    }
}
