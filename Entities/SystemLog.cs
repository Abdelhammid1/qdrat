namespace QdratNew.Entities
{
    public class SystemLog
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string Action { get; set; } // Hide, Restore
        public string Entity { get; set; } // Exam
        public int EntityId { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
