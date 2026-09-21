namespace QdratNew.ViewModels.Students
{
    public class StudentSubjectPerformanceChartViewModel
    {
        public string SubjectName { get; set; }
        public string ChartType { get; set; } = "bar"; // or "pie"
        public List<string> SectionTitles { get; set; } = new();
        public List<double> SuccessRates { get; set; } = new();
        public List<int> Attempts { get; set; } = new();
        public List<double> MaxScores { get; set; } = new();
        public List<double> MinScores { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }
}
