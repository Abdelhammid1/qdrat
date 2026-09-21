namespace QdratNew.ViewModels.Students
{
    public class StudentActivityEntry
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty; // مثل: Homework, Exam, Plan, Session
        public string Description { get; set; } = string.Empty;
    }
}
