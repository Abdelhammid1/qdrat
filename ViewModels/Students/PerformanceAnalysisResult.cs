namespace QdratNew.ViewModels.Students
{
    public class PerformanceAnalysisResult
    {
        public double OverallScore { get; set; }
        public double QuantitativeScore { get; set; }
        public double VerbalScore { get; set; }

        public int StudentId { get; set; }


        // ✅ نقاط القوة عند الطالب
        public List<string> Strengths { get; set; } = new List<string>();

        // ⚠️ نقاط الضعف عند الطالب
        public List<string> Weaknesses { get; set; } = new List<string>();

        // ⏱️ تاريخ إنشاء التحليل
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

    }
}
