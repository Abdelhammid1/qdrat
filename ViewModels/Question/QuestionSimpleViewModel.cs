namespace QdratNew.ViewModels.Question
{
    public class QuestionSimpleViewModel
    {
        public Guid Id { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Title { get; set; }
        public string? LessonTitle { get; set; }
        public bool IsAnswerConfirmed { get; set; }
        public string? CorrectAnswer { get; set; }
        public int? Difficulty { get; set; }
        public string? InternalNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CleanTitle { get; set; }

    }
}
