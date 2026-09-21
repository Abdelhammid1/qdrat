namespace QdratNew.ViewModels.Partner.HomeworkDraft
{
    public class ReplaceHomeworkDraftQuestionVM
    {
        public int DraftId { get; set; }

        public Guid OldQuestionId { get; set; }
        public string OldQuestionTitle { get; set; }

        public int LessonId { get; set; }
        public string LessonTitle { get; set; }

        public List<ReplaceCandidateQuestionVM> Candidates { get; set; } = new();

        // 🔴 كان internal set — هذا سبب الكارثة
        public Guid NewQuestionId { get; set; }
        public List<QuestionCandidateVM> Questions { get;  set; }
    }


    public class ReplaceCandidateQuestionVM
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public int LessonId { get;  set; }
    }
}
