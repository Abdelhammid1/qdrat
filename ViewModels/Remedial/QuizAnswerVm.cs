namespace QdratNew.ViewModels.Remedial
{
    public class QuizAnswerVm
    {
        public Guid QuestionId { get; set; }             // معرف السؤال
        public string SelectedOption { get; set; }       // الإجابة المختارة
        public bool IsCorrect { get; set; }              // هل الإجابة صحيحة؟
    }
}
