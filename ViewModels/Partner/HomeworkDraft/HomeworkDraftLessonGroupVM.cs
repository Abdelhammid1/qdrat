namespace QdratNew.ViewModels.Partner.HomeworkDraft
{
    public class HomeworkDraftLessonGroupVM
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; }
        public List<HomeworkDraftQuestionItemVM> Questions { get; set; } = new();

    
    }

}
