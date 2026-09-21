namespace QdratNew.ViewModels.Homework
{
    public class HomeworkManagementViewModel
    {
        public int HomeworkSetId { get; set; }
        public string BatchName { get; set; }
        public string CompletionTitle { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalStudents { get; set; }
        public int SentCount { get; set; }
        public string AssignedByUser { get; set; }
        public string Status { get; set; } // ✅/❌/⏳
    }
}
