namespace QdratNew.ViewModels.Students
{
    public class SectionPerformanceSummary
    {
        public string SectionTitle { get; set; }
        public double AverageScore { get; set; }
        public double SuccessRate { get; set; }
        public string DifficultyLevel { get; set; }
        public List<SectionPerformanceSummary> SectionSummaries { get; set; } = new List<SectionPerformanceSummary>();
        public int CurriculumId { get; set; } // ✅ جديد
        public string CurriculumTitle { get; set; }


    }
}
