namespace QdratNew.ViewModels.Section
{
    public class SectionAIStatsViewModel
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public string CurriculumTitle { get; set; }

        public int TotalQuestions { get; set; }
        public int TotalAttempts { get; set; }
        public double AverageSuccessRate { get; set; } // 0.0 - 100%

        public int EasyCount { get; set; }
        public int MediumCount { get; set; }
        public int HardCount { get; set; }

        public string BatchName { get; set; } // اختيار الدفعة
    }

}
