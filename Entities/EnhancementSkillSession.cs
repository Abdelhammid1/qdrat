using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class EnhancementSkillSession
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public string SkillType { get; set; } // مثال: "ترتيب العمليات" أو "فهم النص"
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? StudentAnswer { get; set; }
        public bool? IsCorrect => StudentAnswer != null && StudentAnswer == Question.CorrectAnswer;
    }

}
