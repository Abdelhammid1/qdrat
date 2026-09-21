namespace QdratNew.ViewModels.Homework
{
    public class StudentHomeworkSummaryVm
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Required { get; set; }
        public int Late { get; set; }
        public double AverageScore { get; set; }
    }
}
