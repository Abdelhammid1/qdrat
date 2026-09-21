namespace QdratNew.Entities
{
    public class EnhancementSkillResult
    {
        public int Id { get; set; }

        public int EnhancementSkillSetId { get; set; }
        public EnhancementSkillSet EnhancementSkillSet { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }

        public DateTime CompletedAt { get; set; } = DateTime.Now;

    }
}
