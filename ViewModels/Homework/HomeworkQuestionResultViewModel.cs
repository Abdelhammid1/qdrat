namespace QdratNew.ViewModels.Homework
{
    public class HomeworkQuestionResultViewModel
    {
        public Guid QuestionId { get; set; }
        public string QuestionTitle { get; set; }
        public string StudentAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }
    }
}
