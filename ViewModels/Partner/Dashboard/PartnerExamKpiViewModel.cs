namespace QdratNew.ViewModels.Partner.Dashboard
{
    public class PartnerExamKpiViewModel
    {
        public int CompletedExams { get; set; }
        public int LateExams { get; set; }

        public decimal AverageSuccessRate { get; set; }
        public decimal LowestBatchPerformance { get; set; }
    }
}
