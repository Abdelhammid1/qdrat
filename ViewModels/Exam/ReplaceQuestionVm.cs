namespace QdratNew.ViewModels.Exam
{
    public class ReplaceQuestionVm
    {
        public int ExamId { get; set; }
        public Guid OldQuestionId { get; set; }

        public string LessonTitle { get; set; }
        public string Difficulty { get; set; }

        public List<ReplaceQuestionCandidateVm> Candidates { get; set; } = new();
    }

    public class ReplaceQuestionCandidateVm
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; }
        public string ImageUrl { get; set; }
    }

}
