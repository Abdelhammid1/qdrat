namespace QdratNew.ViewModels.Remedial
{
    public class RemedialStudentReportVm
    {
        public string StudentName { get; set; }
        public string PlanTitle { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int TotalVideos { get; set; }
        public int CompletedVideos { get; set; }
        public double TotalWatchMinutes { get; set; }
        public double AverageQuizScore { get; set; }

        public int AttendedSessions { get; set; }
        public int MissedSessions { get; set; }

        public double ProgressPercent { get; set; }

        public string ProgressColor =>
            ProgressPercent >= 80 ? "bg-success"
            : ProgressPercent >= 50 ? "bg-warning text-dark"
            : "bg-danger text-white";

    }
}
