namespace QdratNew.ViewModels.Partner.Exam
{
    public class ExamDashboardVM
    {
        public int TotalExams { get; set; }
        public int CompletedExams { get; set; }
        public int PendingExams { get; set; }

        public double AverageScore { get; set; }

        public List<string> Labels { get; set; } = new();
        public List<int> Data { get; set; } = new();

        public List<ExamAlertVM> Alerts { get; set; } = new();
    }

    public class ExamAlertVM
    {
        public string StudentName { get; set; }
        public string Message { get; set; }
    }
}