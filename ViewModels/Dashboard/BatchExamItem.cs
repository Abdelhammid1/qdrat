namespace QdratNew.ViewModels.Dashboard
{
    public class BatchExamItem
    {
        public int BatchId { get; set; }

        public int ExamId { get; set; }
        public string ExamTitle { get; set; } = "";
        public string CurriculumTitle { get; set; } = "";
        public int StudentsCount { get; set; }
        public int FailedCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
