
namespace QdratNew.ViewModels.Partner
{
    public class PartnerDashboardViewModel
    {
        public string PartnerName { get; set; }
        public string LogoUrl { get; set; }

        public bool IsActive { get; set; }
        public int DaysRemaining { get; set; }

        public int StudentsCount { get; set; }
        public int BatchesCount { get; set; }
        public int CoursesCount { get; set; }

        public bool ShowExpiryWarning => DaysRemaining <= 30 && DaysRemaining > 0;

        public List<PartnerCourseContext> Courses { get; set; }
        public int ActiveStudents { get; set; }
        public int ArchivedStudents { get; set; }
        public int TotalStudents { get; set; }
        public int TotalBatches { get; set; }
        public int StudentsWithoutBatch { get; set; }
        public List<string> LatestStudents { get; set; }
    }
}
