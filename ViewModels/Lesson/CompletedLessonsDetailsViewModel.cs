using QdratNew.ViewModels.Students;

namespace QdratNew.ViewModels.Lesson
{
    public class CompletedLessonsDetailsViewModel
    {
        public int BatchId { get; set; }
        public string CompletionTitle { get; set; }
        public List<QdratNew.ViewModels.Students.StudentCompletionDetailViewModel> StudentDetails { get; set; } = new();

        public List<StudentCompletionDetailViewModel> Students { get; set; } = new();
        // ⬅️ أضف هذا لو تعرض تفاصيل طالب واحد:
        public List<StudentHomeworkDetailViewModel> HomeworkDetails { get; set; } = new();



        public string StudentName { get; set; } // ✅ اسم الطالب
        public string BatchName { get; set; }   // ✅ اسم الدفعة

        public int QuestionCount { get; set; }
        public int AnsweredCount { get; set; }
        public int CorrectCount { get; set; }
        public double Percentage { get; set; }
        public string Status { get; set; }



    }
}
