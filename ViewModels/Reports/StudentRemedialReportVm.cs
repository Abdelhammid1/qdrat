namespace QdratNew.ViewModels.Reports
{
    public class StudentRemedialReportVm
    {
        public int StudentId { get; set; }
        public int SessionId { get; set; }
        public int TotalVideos { get; set; }
        public int VideosWatched { get; set; }
        public double AverageQuizScore { get; set; }
        public double CompletionPercent { get; set; }
    }
}
