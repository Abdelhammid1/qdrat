using System;

namespace QdratNew.ViewModels.Branches
{
    public class BranchAIReport
    {
        public int BranchId { get; set; }

        public string BranchName { get; set; }

        public int TotalStudents { get; set; }

        public int TotalCourses { get; set; }

        public int TotalProjects { get; set; }

        public double AveragePerformance { get; set; }

        public string AiComment { get; set; }

        public string Recommendation { get; set; }

        public DateTime EstablishedDate { get; set; } // ✅ أضف هذا
        public string City { get; set; } // ✅ وأيضًا هذا للخطأ التالي
        public bool IsPartner { get; set; }

    }
}
