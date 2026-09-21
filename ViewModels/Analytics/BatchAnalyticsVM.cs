namespace QdratNew.ViewModels.Admin.Analytics
{
    public class BatchAnalyticsVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }

        public string CourseName { get; set; }

        public int WeakLessonsCount { get; set; }

        public int AffectedStudents { get; set; }

        public double AvgWeakness { get; set; }

        public string RiskLevel { get; set; }
        public int StudentsCount { get; internal set; }
        public int CriticalLessonsCount { get; internal set; }
    }
}