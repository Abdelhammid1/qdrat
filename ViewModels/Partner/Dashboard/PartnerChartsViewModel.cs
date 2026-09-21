namespace QdratNew.ViewModels.Partner.Dashboard
{
    public class PartnerChartsViewModel
    {
        public List<ChartPointViewModel> StudentPerformanceOverTime { get; set; } = new();
        public List<ChartBarViewModel> BatchPerformanceComparison { get; set; } = new();
        public List<ChartSegmentViewModel> StudentLevelDistribution { get; set; } = new();
        public List<StackedBarViewModel> HomeworkCommitment { get; set; } = new();
        public List<HorizontalBarViewModel> InstructorActivity { get; set; } = new();
    }
}
