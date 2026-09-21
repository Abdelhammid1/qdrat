namespace QdratNew.Entities
{
    public class StudentAnswer
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public Student Student { get; set; }

        public Guid QuestionId { get; set; }
        public Question Question { get; set; }

        public bool IsCorrect { get; set; }

        public DateTime AnsweredAt { get; set; } = DateTime.Now;

        // خصائص إضافية مثل الخيار المختار وغيرها...
    }

}
