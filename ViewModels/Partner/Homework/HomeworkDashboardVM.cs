namespace QdratNew.ViewModels.Partner.Homework
{
    public class HomeworkDashboardVM
    {
        public int TotalHomeworks { get; set; }
        public int CompletedHomeworks { get; set; }
        public int PendingHomeworks { get; set; }

        public double CompletionRate { get; set; }

        public List<string> Labels { get; set; } = new();
        public List<int> Data { get; set; } = new();

        public List<HomeworkAlertVM> Alerts { get; set; } = new();
    }

    public class HomeworkAlertVM
    {
        public string StudentName { get; set; }
        public string Message { get; set; }
    }

}
