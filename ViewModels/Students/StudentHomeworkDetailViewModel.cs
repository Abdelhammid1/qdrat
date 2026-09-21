namespace QdratNew.ViewModels.Students
{
    public class StudentHomeworkDetailViewModel
    {
        public string QuestionText { get; set; }
        public string? StudentAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }


        public Guid QuestionId { get; set; }
       


    }
}
