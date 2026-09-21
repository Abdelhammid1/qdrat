namespace QdratNew.ViewModels.Reports
{
    public class IndicatorPerformanceReportViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }

        public string SectionTitle { get; set; }
        public double ScorePercent { get; set; }
        public bool IsPassed { get; set; }

        public string? FailureReason { get; set; }
        public bool HasRemedialPlan { get; set; }
        public string? RemedialPlanTitle { get; set; }
        public DateTime? RemedialStartDate { get; set; }
        public DateTime? RemedialEndDate { get; set; }
    }
}
