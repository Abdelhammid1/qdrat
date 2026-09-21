namespace QdratNew.ViewModels.Instructor.Exam
{
    public class LessonGenerateVM
    {
        public int LessonId { get; set; }
        public string LessonName { get; set; }

        public int AvailableQuestionsCount { get; set; } // 🔥 إحصائية
        public int SelectedCount { get; set; } // 🔥 عدد المطلوب
    }
}
