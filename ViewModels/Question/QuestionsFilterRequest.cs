namespace QdratNew.ViewModels.Question
{
    public class QuestionsFilterRequest
    {
        public int Draw { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }

        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int? LessonId { get; set; }

        public string? SearchTitle { get; set; }
    }
}
