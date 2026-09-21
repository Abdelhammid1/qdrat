namespace QdratNew.ViewModels.Homework
{
    public class HomeworkBatchCardVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string CourseTitle { get; set; }
        public int TotalStudents { get; set; }
        public int TotalHomeworks { get; set; }
        public int TotalAssigned { get; set; }
        public int TotalSubmitted { get; set; }
        public int SolvedPercentage { get; set; }
        public int Late24hCount { get; set; }
        public int Critical3daysCount { get; set; }
    }
}
