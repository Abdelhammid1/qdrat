namespace QdratNew.Services.MLModels
{
    public class HomeworkPerformanceInput
    {
        public float AverageScore { get; set; }
        public float ErrorRate { get; set; }
        public float CompletionRate { get; set; }
    }

    public class HomeworkPerformanceOutput
    {
        public string Recommendation { get; set; }
    }
}
