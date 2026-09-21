namespace QdratNew.ViewModels.Admin.Analytics
{
    public class LessonAnalyticsVM
    {
        public int LessonId { get; set; }
        public string LessonName { get; set; }

        public string BatchName { get; set; }
        public string InstructorName { get; set; }
        public int AffectedStudents { get; set; }

        public int TotalAttempts { get; set; }
        public int StudentsCount { get; set; }

        public double WeakPercentage { get; set; }
        public int? BatchId { get; set; }
    }
}