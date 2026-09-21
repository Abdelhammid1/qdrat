namespace QdratNew.ViewModels.Partner.HomeworkDraft
{
    public class AddHomeworkDraftQuestionVM
    {
        public int DraftId { get; set; }
        public int LessonId { get; set; }

        public List<ReplaceCandidateQuestionVM> Questions { get; set; }
            = new();
    }
}
