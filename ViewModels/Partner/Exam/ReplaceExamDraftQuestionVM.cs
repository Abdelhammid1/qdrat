namespace QdratNew.ViewModels.Partner.Exam
{
    public class ReplaceExamDraftQuestionVM
    {
        public int DraftId { get; set; }
        public Guid OldQuestionId { get; set; }

        public int? SectionId { get; set; }
        public string SectionTitle { get; set; }

        public List<ReplaceCandidateQuestionVM> Candidates { get; set; }
            = new();

        public int LessonId { get; set; }
        public string LessonTitle { get; set; }

        public string OldQuestionTitle { get; set; }




    }

    public class ReplaceCandidateQuestionVM
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; } = "";
    }
}
