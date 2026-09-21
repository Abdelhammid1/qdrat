namespace QdratNew.ViewModels.Students
{
    public class StudentMentorDashboardViewModel
    {
        public string FullName { get; set; }
        public float SuccessRate { get; set; }
        public float SessionCompletionRatio { get; set; }
        public float AverageHomeworkTime { get; set; }
        public string AssessedLevel { get; set; }
        public float RiskScore { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public float ProgressPercentage { get; set; } // داخل StudentMentorDashboardViewModel
        public DateTime? LastAnalysisDate { get; set; }

        public int TotalAssignments { get; set; }
        public int TotalExams { get; set; }




    }
}
