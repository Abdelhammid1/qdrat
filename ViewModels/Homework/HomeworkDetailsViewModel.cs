namespace QdratNew.ViewModels.Homework
{
    public class HomeworkDetailsViewModel
    {
        public int HomeworkSetId { get; set; }
        public DateTime CreatedAt { get; set; }
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string BehaviorFlag { get; set; } // Normal / Suspicious / CheatingLikely
        public double AvgTimePerQuestion { get; set; }
        public int FastAnswersCount { get; set; }
        public ParentReportResult ParentReport { get; set; }
        public string ParentReportSummary { get; set; }
        public List<HomeworkStudentViewModel> Students { get; set; }
    }
}
