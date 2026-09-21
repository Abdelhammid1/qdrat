namespace QdratNew.ViewModels.Reports
{
    public class ReviewQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionTitle { get; set; }
        public bool IsCorrect { get; set; }

        // في حالة لاحقًا أردنا عرض الإجابة المختارة أو الصحيحة
        public string SelectedOptionText { get; set; }
        public string CorrectOptionText { get; set; }


        // 🆕 الصورة الخاصة بالسؤال (قد تكون نصية أو رسومية)
        public string ImageUrl { get; set; }

        // 🆕 نوع العرض (اختياري لدعم المقارنة لاحقًا)
        public string DisplayType { get; set; }
        public string SelectedAnswer { get; internal set; }
        public bool IsQuantitative { get; internal set; }
        public string CorrectAnswer { get; internal set; }
    }
}
