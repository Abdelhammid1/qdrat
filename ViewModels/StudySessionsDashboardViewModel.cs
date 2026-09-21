using QdratNew.Enums;
using QdratNew.ViewModels.Shared;

namespace QdratNew.ViewModels
{
    public class StudySessionsDashboardViewModel
    {
        // الكروت المالية
        public decimal TotalRevenue { get; set; }
        public int TotalSessions { get; set; }
        public decimal AverageRevenuePerSession { get; set; }

        // الرسم البياني للعائد
        public List<ChartDataPoint> RevenueOverTimeChart { get; set; }

        // توزيع حسب الحالة
        public Dictionary<string, int> SessionsByStatus { get; set; }

        // ✅ الكروت الجديدة للإحصائيات
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int PendingCount { get; set; }

        // ✅ الرسم البياني لتفاعل المدربين
        public List<ChartDataPoint> TrainerEngagementChartData { get; set; }

        // تحليل AI
        public string AIRecommendation { get; set; }



    }

    public class TrainerEngagementChartItem
    {
        public string TrainerName { get; set; }
        public int SessionCount { get; set; }
    }

    public class RevenueChartPoint
    {
        public string DateLabel { get; set; }
        public double Revenue { get; set; }
    }
}
