namespace QdratNew.ViewModels.Partner.Reports
{
    public class PartnerDashboardVM
    {
        // الطلاب
        public int TotalStudents { get; set; }
        public int ActiveBatches { get; set; }

        // الاختبارات
        public int TotalSentExams { get; set; }
        public int TotalSolvedExams { get; set; }
        public int TotalUnsolvedExams { get; set; }

        // الأداء
        public double AverageScorePercent { get; set; }
    }

}
