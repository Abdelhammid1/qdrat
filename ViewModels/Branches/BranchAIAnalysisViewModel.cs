using QdratNew.ViewModels.Analytics;

namespace QdratNew.ViewModels.Branches
{
    // Namespace: QdratNew.ViewModels.Branches
    public class BranchAIAnalysisViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string City { get; set; }
        public string Type => IsPartner ? "شراكة" : "مباشر";
        public bool IsPartner { get; set; }
        public int TotalStudents { get; set; }
        public int TotalCourses { get; set; }
        public int TotalProjects { get; set; }
        public double AveragePerformance { get; set; }

        public List<PerformancePointViewModel> MonthlyPerformanceTrend { get; set; } = new();
        public List<string> AIRecommendations { get; set; } = new();



    }

}
