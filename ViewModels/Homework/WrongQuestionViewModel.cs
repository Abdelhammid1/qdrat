using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels.Homework
{
    public class WrongQuestionViewModel
    {
        public string QuestionTitle { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? StudentAnswer { get; set; }
        public string? VideoUrl { get; set; }

        public QuestionDisplayViewModel Question { get; set; } = new();  // السؤال نفسه
        public string SelectedAnswer { get; set; } = string.Empty;       // الإ
        public List<QuestionOptionDisplayViewModel> Options { get; set; } = new();



    }

}
