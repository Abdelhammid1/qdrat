using QdratNew.AI.MLModels.Branches;
using QdratNew.ViewModels.Shared;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Branches
{
    public class BranchesDashboardViewModel
    {
        public string FullName { get; set; }

        public List<BranchAIReport> BranchReports { get; set; } = new();

        // 📊 بيانات الاتجاه الزمني
        public List<ChartTimeSeriesData> MonthlyStudentCounts { get; set; } = new();
        public List<ChartTimeSeriesData> MonthlyAveragePerformance { get; set; } = new();
        public List<ChartTimeSeriesData> MonthlyCourseCounts { get; set; } = new();

        // 🔮 توقعات الذكاء الاصطناعي
        public List<float> StudentForecast { get; set; } = new();
        public List<float> PerformanceForecast { get; set; } = new();
        public List<float> CoursesForecast { get; set; } = new();

        // ⏱ توقعات زمنية مفصلة
        public List<TimeSeriesForecastPoint> PerformanceForecastPoints { get; set; } = new();








    }
}
