namespace QdratNew.ViewModels.Homework
{
    public class HomeworkReportQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string StudentAnswer { get; set; }
        public bool IsCorrect { get; set; }
    }
}
