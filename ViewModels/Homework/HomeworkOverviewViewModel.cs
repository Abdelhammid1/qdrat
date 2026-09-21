namespace QdratNew.ViewModels.Homework
{
    public class HomeworkOverviewViewModel
    {
        public int HomeworkSetId { get; set; }
        public string BatchName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int QuestionCount { get; set; }
        public int StudentCount { get; set; }
        public bool HasBeenResent { get; set; } // ✅ الجديد
        public string CompletionTitle { get; set; } // ✅ جديدة
        public bool IsArchived { get; set; }


    }
}
