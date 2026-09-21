namespace QdratNew.ViewModels.Analysis
{
    public class BatchSectionHomeworkStatsViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int TotalStudents { get; set; }
        public int StudentsSubmitted { get; set; }
        public double AverageScore { get; set; }
    }
}
