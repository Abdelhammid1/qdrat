namespace QdratNew.ViewModels.AI
{
    public class ChartAIResult
    {
        public float RiskScore { get; set; }  // مؤشر خطورة الأداء (0 إلى 100)
        public string AssessedLevel { get; set; }  // "ضعيف", "متوسط", "متميز"
        public List<string> Recommendations { get; set; } = new();
    }
}
