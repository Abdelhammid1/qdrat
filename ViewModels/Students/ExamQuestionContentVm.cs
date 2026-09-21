using QdratNew.ViewModels.Question;

namespace QdratNew.ViewModels.Students
{
    // Shared question presentation; each exam keeps its existing answer binding contract.
    public class ExamQuestionContentVm
    {
        public QuestionDisplayViewModel Question { get; set; } = null!;
        public string OptionFieldName { get; set; } = "SelectedOption";
        public string? SelectedAnswer { get; set; }
        public bool UseOptionIndex { get; set; }
        public int? SelectedOptionIndex { get; set; }
    }
}
