namespace QdratNew.ViewModels.Homework
{
    public class ParentReportResult
    {
        public string Level { get; set; }
        public string BehaviorText { get; set; }
        public string Commitment { get; set; }
        public string Progress { get; set; }
        public List<string> HomeRecommendations { get; set; }


        // ✅ أضف هذا السطر (حل المشكلة)
        public string ParentInsight { get; set; }
        public string Summary { get; internal set; }
        public string Recommendation { get; internal set; }
    }
}
