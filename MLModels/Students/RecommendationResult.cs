namespace QdratNew.MLModels.Students
{
    public class RecommendationResult
    {
        public float PredictedScore { get; set; }
        public string Level { get; set; } = "غير محدد";
        public string Recommendation { get; set; } = "";
    }
}
