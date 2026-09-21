using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class StudentExamResult
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public bool IsCorrect { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}
