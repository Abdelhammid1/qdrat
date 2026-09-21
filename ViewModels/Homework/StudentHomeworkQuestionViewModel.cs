namespace QdratNew.ViewModels.Homework
{
    public class StudentHomeworkQuestionViewModel
    {
        public int HomeworkId { get; set; }

        public string QuestionText { get; set; }

        public string[] Choices { get; set; }

        public string ImageUrl { get; set; }

        public string StudentAnswer { get; set; }

        public string CorrectAnswer { get; set; } // اختياري، يظهر بعد التسليم

        public bool? IsCorrect { get; set; } // اختياري، يظهر بعد التصحيح

        public int Order { get; set; } // للترتيب داخل الواجهة (اختياري)

        public bool IsReadOnly { get; set; } = false; // هل السؤال مغلق (للاستعراض فقط)
    }
}
