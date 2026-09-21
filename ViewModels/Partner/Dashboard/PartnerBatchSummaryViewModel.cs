namespace QdratNew.ViewModels.Partner.Dashboard
{
    public class PartnerBatchSummaryViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;

        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }

        public decimal AveragePerformance { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}
