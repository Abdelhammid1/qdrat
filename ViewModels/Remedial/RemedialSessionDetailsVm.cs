namespace QdratNew.ViewModels.Remedial
{
    public class RemedialSessionDetailsVm
    {
        public int SessionId { get; set; }
        public string StudentName { get; set; }
        public string PlanTitle { get; set; }
        public string AccessCode { get; set; }
        public DateTime CreatedAt { get; set; }

        public int TotalWatchedSeconds { get; set; }
        public int TotalQuizAttempts { get; set; }
        public double AverageScore { get; set; }
    }

}
