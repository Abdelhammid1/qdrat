using System;

namespace QdratNew.Entities
{
    public class EnhancementSkillAssignment
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.Now;

        public string? StudentAnswer { get; set; }

        public bool? IsCorrect { get; set; }

        public DateTime? AnsweredAt { get; set; }
  

        public int EnhancementSkillSetId { get; set; }
        public EnhancementSkillSet EnhancementSkillSet { get; set; }

     
    }
}
