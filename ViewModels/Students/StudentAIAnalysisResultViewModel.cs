namespace QdratNew.ViewModels.Students
    {
    public class StudentAIAnalysisResultViewModel
    {
        public float SuccessRate { get; set; }
        public float SessionCompletionRatio { get; set; }
        public float AverageHomeworkTime { get; set; }
        public int TotalAssignments { get; set; }
        public int TotalExams { get; set; }
        public string AssessedLevel { get; set; } = string.Empty;
        public float RiskScore { get; set; }
        public DateTime AnalyzedAt { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }
}


