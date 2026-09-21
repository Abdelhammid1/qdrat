namespace QdratNew.ViewModels.Partner.Homework
{
    public class HomeworkDraftPreviewLessonVM
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }

        public List<HomeworkDraftPreviewQuestionVM> Questions { get; set; }
            = new();
    }
}
