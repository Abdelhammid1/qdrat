using QdratNew.ViewModels.Shared;

namespace QdratNew.ViewModels.AI
{
    public class ChartAnalysisInputViewModel
    {
        public string ChartId { get; set; }
        public string Title { get; set; } = string.Empty;

        public float SuccessRate { get; set; }
        public float SessionCompletionRatio { get; set; }
        public float AverageHomeworkTime { get; set; }
        public int TotalAssignments { get; set; }
        public float TotalExams { get; set; }

        public float RiskScore { get; set; } // ← أضف هذا إذا غير موجود

    
        public List<ChartDataPoint> DataPoints { get; set; } = new List<ChartDataPoint>();
    }
}
