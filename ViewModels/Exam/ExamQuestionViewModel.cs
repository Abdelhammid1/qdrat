namespace QdratNew.ViewModels.Exam
{
    public class ExamQuestionViewModel
    {
        public Guid QuestionId { get; set; } // ✅ أضف هذا السطر

        public int Order { get; set; }

        public string QuestionText { get; set; }

        public string LessonTitle { get; set; }

        public string SectionTitle { get; set; }

        public string Difficulty { get; set; }

        public bool IsManual { get; set; }




    }
}
