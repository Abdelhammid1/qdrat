namespace QdratNew.ViewModels.Section
{
    public class SectionEvaluationAIViewModel
    {
        public string SectionTitle { get; set; }
        public double AverageScore { get; set; }
        public double SuccessRate { get; set; }
        public int StudentCount { get; set; }
        public string DifficultyLevel { get; set; }  // صعب - متوسط - سهل (بناء على الأداء)
        public string AIComment { get; set; }


        // نسبة التحسن مقارنة بالفترة السابقة (اختياري)
        public double ProgressRate { get; set; }  // مثال: +12.5%

        // عدد المحاولات في الأسئلة الخاصة بالمحور
        public int AttemptsCount { get; set; }

    }
}
