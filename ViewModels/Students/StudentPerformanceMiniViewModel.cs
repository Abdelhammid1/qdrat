namespace QdratNew.ViewModels.Students
{
    public class StudentPerformanceMiniViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public double Score { get; set; }
        public float EngagementRate { get; set; }
        public int AttendanceCount { get; set; }

        public string? WeakTopics { get; set; }
    }
}
