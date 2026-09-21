using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Branches
{
    public class BranchDashboardViewModel
    {
        public List<BranchCardViewModel> BranchCards { get; set; } = new();
        public List<BranchChartData> PerformanceChartData { get; set; } = new();
        public List<BranchChartData> StudentCountChartData { get; set; } = new();
    }

    public class BranchCardViewModel
    {
        public int Id { get; set; }
        public string BranchName { get; set; }
        public int TotalStudents { get; set; }
        public int TotalCourses { get; set; }
        public int TotalProjects { get; set; }
        public double AveragePerformance { get; set; }
        public string AiComment { get; set; }
        public string Recommendation { get; set; }
    }

    public class BranchChartData
    {
        public string Label { get; set; } // اسم الفرع
        public double Value { get; set; } // القيمة: عدد أو نسبة
    }
}
