namespace QdratNew.Services.Exams.Models
{
    public class GeneratedExamResult
    {
        public string ExamTitle { get; set; }
        public int CourseId { get; set; }

        public List<GeneratedQuestionItem> Questions { get; set; }
            = new();
    }

    public class GeneratedQuestionItem
    {
        public Guid QuestionId { get; set; }
        public int CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int? LessonId { get; set; }
        public string? Title { get; internal set; }
        public string LessonTitle { get; internal set; }
        public string SectionTitle { get; internal set; }
        public string Difficulty { get; internal set; }
    }
}
