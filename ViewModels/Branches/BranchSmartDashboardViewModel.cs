using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Branches
{
    public class BranchSmartDashboardViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string City { get; set; }
        public bool IsPartner { get; set; }
        public DateTime EstablishedDate { get; set; }

        public int TotalStudents { get; set; }
        public int TotalCourses { get; set; }
        public int TotalProjects { get; set; }

        public double AveragePerformance { get; set; }
        public double AverageEngagement { get; set; }
        public double AverageAttendance { get; set; }

        public List<string> CommonWeakTopics { get; set; } = new();
        public List<PerformancePointViewModel> MonthlyPerformanceTrend { get; set; } = new();

        public string AiComment { get; set; }
        public string Recommendation { get; set; }
    }

    public class PerformancePointViewModel
    {
        public string Month { get; set; } // yyyy-MM
        public double AverageScore { get; set; }
    }
}
