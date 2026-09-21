namespace QdratNew.ViewModels.Remedial
{
    public class RemedialSessionTrackingSummaryVm
    {
        public int SecondsWatched { get; set; }          // إجمالي مدة المشاهدة بالثواني
        public int QuizzesCompleted { get; set; }        // عدد الاختبارات التطبيقية التي خاضها الطالب
        public double AverageScore { get; set; }         // متوسط نتائج الاختبارات

        public int SessionId { get; set; }
        public int TotalVideos { get; set; }
        public int CompletedVideos { get; set; }
        public double TotalWatchMinutes { get; set; }
        public double AverageQuizScore { get; set; }
        public double ProgressPercent { get; set; }

        public string ProgressColor =>
            ProgressPercent >= 80 ? "bg-success" :
            ProgressPercent >= 50 ? "bg-warning text-dark" :
            "bg-danger text-white";






    }

}
