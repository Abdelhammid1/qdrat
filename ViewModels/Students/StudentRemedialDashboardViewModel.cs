namespace QdratNew.ViewModels.Students
{
    public class StudentRemedialDashboardViewModel
    {
        public RemedialPlanViewModel? CurrentPlan { get; set; }
        public StudentSessionReservationListViewModel? NextSession { get; set; }
        public List<StudentSessionReservationListViewModel> PastSessions { get; set; }
        public List<string> AIRecommendations { get; set; }
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public int PendingSessions { get; set; }
    }

}
