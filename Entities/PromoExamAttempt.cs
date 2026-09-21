using System;

namespace QdratNew.Entities
{
    public class PromoExamAttempt
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public PromoExamSession Session { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public string? SelectedAnswer { get; set; }   // ← لاحظ علامة الاستفهام
        public bool IsCorrect { get; set; }
        public DateTime AttemptedAt { get; set; } = DateTime.Now;
    }
}
