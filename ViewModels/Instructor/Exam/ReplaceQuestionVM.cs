namespace QdratNew.ViewModels.Instructor.Exam
{
    public class ReplaceQuestionVM
    {
        public Guid Id { get; set; }

        public string Title { get; set; }

        // اختياري (للتوسعة لاحقًا)
        public int SectionId { get; set; }

        public int LessonId { get; set; }
        public string SectionTitle { get; set; }
        public string LessonTitle { get; set; }
        public string Difficulty { get; set; }
        public int CurriculumId { get; set; }
    }
}