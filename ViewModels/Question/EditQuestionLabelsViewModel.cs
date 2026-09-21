namespace QdratNew.ViewModels.Question
{
    public class EditQuestionLabelsViewModel
    {
        public int QuestionId { get; set; }

        // التصنيفات الحالية المرتبطة بالسؤال
        public List<string> SelectedLabels { get; set; } = new List<string>();

        // جميع التصنيفات المتاحة في النظام
        public List<string> AllLabels { get; set; } = new List<string>();
    }
}
