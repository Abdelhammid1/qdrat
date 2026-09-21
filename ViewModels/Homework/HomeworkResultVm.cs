namespace QdratNew.ViewModels.Homework
{
    public class HomeworkResultVm
    {
        public Guid QuestionId { get; set; }
        public bool IsCorrect { get; set; }
        public string SelectedAnswer { get; set; }   // الإجابة اللي اختارها الطالب
        public string CorrectAnswer { get; set; }    // الإجابة الصحيحة
        public string QuestionText { get; set; }     // نص السؤال
    }
}
