namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class SnapshotComparisonVM
    {
        public double? SnapshotAvgExamScore { get; set; }
        public double? CurrentAvgExamScore  { get; set; }
        public double? SnapshotAtRiskCount  { get; set; }
        public double? CurrentAtRiskCount   { get; set; }
        public int DaysSinceDecision        { get; set; }

        public string TrendLabel    => ResolveTrendLabel();
        public string TrendCssClass => ResolveTrendCss();

        private string ResolveTrendLabel()
        {
            if (!SnapshotAvgExamScore.HasValue || !CurrentAvgExamScore.HasValue)
                return "ثبات";
            var delta = CurrentAvgExamScore.Value - SnapshotAvgExamScore.Value;
            return delta > 2.0 ? "تحسن" : delta < -2.0 ? "تراجع" : "ثبات";
        }

        private string ResolveTrendCss() => TrendLabel switch
        {
            "تحسن"  => "success",
            "تراجع" => "danger",
            _       => "secondary"
        };
    }
}
