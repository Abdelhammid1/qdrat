using System;
using System.Collections.Generic;
using QdratNew.ViewModels.Students;

namespace QdratNew.ViewModels.Branches
{
    public class BranchSmartReportViewModel
    {
        // ✅ بيانات الفرع الأساسية
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string City { get; set; }
        public bool IsPartner { get; set; }
        public DateTime EstablishedDate { get; set; }

        // ✅ مؤشرات إجمالية
        public int TotalStudents { get; set; }
        public int TotalCourses { get; set; }
        public int TotalProjects { get; set; }
        public double AveragePerformance { get; set; }

        // ✅ تحليل الأداء الزمني (شهريًا)
        public List<PerformancePointViewModel> MonthlyPerformanceTrend { get; set; } = new();

        // ✅ تحليل الذكاء الاصطناعي
        public string AiComment { get; set; }
        public string Recommendation { get; set; }

        // ✅ التصنيف الآلي للأداء
        public string PerformanceLevel { get; set; }

        // ✅ توزيع الدرجات داخل الفرع (لـ PieChart أو تحليل توزيعي)
        public List<double> ScoreDistribution { get; set; } = new();

        // ✅ إذا احتجنا تفاصيل إضافية لكل طالب
        public List<StudentPerformanceMiniViewModel> TopStudents { get; set; } = new();


        public double MLBasedScorePrediction { get; set; } = 0;

        public double AverageEngagement { get; set; }
        public double AverageAttendance { get; set; }

      


    }
}
