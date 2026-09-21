namespace QdratNew.ViewModels.Students
{
    public class ComparisonPerformanceEntry
    {
        public string Label { get; set; }
        public DateTime Date { get; set; }
        public int Score { get; set; }
        public string Type { get; set; } // "اختبار" أو "واجب"
    }
}
