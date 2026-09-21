namespace QdratNew.ViewModels.Admin.Analytics
{
    public class InstructorAnalyticsVM
    {
        public int InstructorId { get; set; }
        public string InstructorName { get; set; }

        public int BatchesCount { get; set; }

        public int WeakLessonsCount { get; set; }

        public double AvgScore { get; set; }

        public string RiskLevel { get; set; }
    }
}