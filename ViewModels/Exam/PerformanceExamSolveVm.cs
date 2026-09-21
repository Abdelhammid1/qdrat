using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels.Exam
{
    public class PerformanceExamSolveVm
    {
        public int PerformanceIndicatorExamId { get; set; }
        public string ExamTitle { get; set; }
        public Guid CurrentQuestionId { get; set; }
        public List<Guid> AllQuestionIds { get; set; }
        public QuestionDisplayViewModel Question { get; set; }
        public string SelectedAnswer { get; set; }
        public int RemainingSeconds { get; set; }
        public bool IsSubmitted { get; set; }
    }
}
