using QdratNew.ViewModels.Partner.Risk;

namespace QdratNew.ViewModels.Partner.Dashboard
{
 
        public class PartnerDashboardAdvancedVM
        {
        public List<StudentRiskVM> RiskStudents { get; set; } = new();
        // ================= KPI =================
        public int TotalStudents { get; set; }
            public int ActiveStudents { get; set; }
            public int ArchivedStudents { get; set; }
            public int TotalBatches { get; set; }

            // ================= Health =================
            public int HighPerformingStudents { get; set; }
            public int AtRiskStudents { get; set; }
            public int InactiveStudents { get; set; }

            // ================= Activity =================
            public int StudentsWithNoBatch { get; set; }
            public int StudentsWithoutActivity { get; set; }

            // ================= Lists =================
            public List<string> LatestStudents { get; set; } = new();

            // ================= Charts =================
            public int QuantitativeStudents { get; set; }
            public int VerbalStudents { get; set; }

        public List<string> CourseLabels { get; set; } = new();
        public List<int> CourseCounts { get; set; } = new();
    }
   
}
