using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels.Exam
{
    public class ExamReviewQuestionViewModel
    {
        public Guid QuestionId { get; set; }
        public string QuestionTitle { get; set; }
        public string ImageUrl { get; set; }
        public string CorrectAnswer { get; set; }
        public string StudentAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsQuantitative { get; set; }

        public List<QuestionOptionDisplayViewModel> Options { get; set; } = new();



    }
}
