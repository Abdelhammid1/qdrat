namespace QdratNew.ViewModels.Partner.Dashboard
{
    public class PartnerStudentKpiViewModel
    {
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int AtRiskStudents { get; set; }
        public int NotStartedStudents { get; set; }

        public decimal ActivePercentage { get; set; }
    }
}
