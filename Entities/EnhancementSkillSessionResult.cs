using System;

namespace QdratNew.Entities
{
    public class EnhancementSkillSessionResult
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int BatchId { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public DateTime CompletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;


        public int EnhancementSkillSetId { get; set; }
        public EnhancementSkillSet EnhancementSkillSet { get; set; }

        public Student Student { get; set; }



    }
}
