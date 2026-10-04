using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    /// <summary>RTK: سؤال مجمّد داخل محاولة + إجابة الطالب.</summary>
    public class RemedialTrackExamAttemptQuestion
    {
        public int Id { get; set; }
        public int AttemptId { get; set; }
        public RemedialTrackExamAttempt? Attempt { get; set; }
        public Guid QuestionId { get; set; }
        public Question? Question { get; set; }
        public int Order { get; set; }
        [MaxLength(1000)] public string? SelectedAnswer { get; set; }
        public bool? IsCorrect { get; set; }                 // null حتى التسليم
        public DateTime? AnsweredAtUtc { get; set; }
    }
}
