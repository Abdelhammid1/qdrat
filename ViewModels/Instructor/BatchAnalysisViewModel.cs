namespace QdratNew.ViewModels.Instructor
{
    public class BatchAnalysisViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public int StudentCount { get; set; }
        public double AvgScore { get; set; }
        public int CompletionRate { get; set; }
        public int ActiveHomeworks { get; set; }
    }
}
