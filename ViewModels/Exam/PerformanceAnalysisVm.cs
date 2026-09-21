namespace QdratNew.ViewModels.Exam
{
    public class PerformanceAnalysisVm
    {
        public double OverallScore { get; set; }
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();



        // ✅ خاصية جديدة لتغذية الرسوم البيانية في الداشبورد
        public List<ChartDataVm> ChartData { get; set; } = new();
        public int AverageSolveTime { get; internal set; }
        public int AverageExamDuration { get; internal set; }
    }

    // ✅ نموذج صغير لتمثيل بيانات الشارت
    public class ChartDataVm
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }



}
