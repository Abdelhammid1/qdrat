namespace QdratNew.Entities
{
    public class HomeworkSetAttempt
    {
        public int Id { get; set; }
        public int HomeworkSetId { get; set; }
        public int StudentId { get; set; }
        public int AttemptNumber { get; set; } // 1,2,...

        public DateTime StartedAt { get; set; } = DateTime.Now;
        public DateTime? SubmittedAt { get; set; }

        public HomeworkSet HomeworkSet { get; set; }
        public Student Student { get; set; }
    }

}
