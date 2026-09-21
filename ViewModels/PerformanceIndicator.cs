namespace QdratNew.ViewModels.PerformanceIndicator
{
    public class PerformanceIndicatorDashboardVm
    {
        public int TotalExams { get; set; }
        public int CompletedExams { get; set; }
        public double AverageScore { get; set; }

        public List<PerformanceIndicatorExamCardVm> Exams { get; set; } = new();


   
    }


    public class PerformanceIndicatorExamItemVm
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public string Curriculum { get; set; }
        public DateTime CreatedAt { get; set; }

        public bool IsCompleted { get; set; }
        public double ScorePercent { get; set; }

        public string StatusText { get; set; }


   
    }





    public class PerformanceIndicatorExamCardVm
    {
        public int ExamId { get; set; }
        public string Title { get; set; }
        public string Curriculum { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsCompleted { get; set; }
        public double ScorePercent { get; set; }
        public string StatusText { get; set; }
    }
}
