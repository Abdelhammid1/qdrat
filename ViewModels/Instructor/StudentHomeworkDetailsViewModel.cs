namespace QdratNew.ViewModels.Instructor
{
    public class StudentHomeworkDetailsViewModel
    {
        public string StudentName { get; set; }
        public string HomeworkSetTitle { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public List<HomeworkQuestionViewModel> Questions { get; set; }
    }

    public class HomeworkQuestionViewModel
    {
        public string QuestionTitle { get; set; }
        public string StudentAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public bool? IsCorrect { get; set; }
        public string? Explanation { get; set; }
        public string? VideoUrl { get; set; }
    }

}
