namespace QdratNew.Services.Exams.Internal
{
    public class GeneratedQuestionItem
    {
        public Guid QuestionId { get; set; }

        public int CurriculumId { get; set; }

        public int? SectionId { get; set; }
        public string SectionTitle { get; set; }

        public int? LessonId { get; set; }
        public string LessonTitle { get; set; }

        public string Title { get; set; }
        public string Difficulty { get; set; }
    }
}
