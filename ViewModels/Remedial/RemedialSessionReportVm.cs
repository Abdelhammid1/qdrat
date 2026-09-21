namespace QdratNew.ViewModels.Remedial
{
    public class RemedialSessionReportVm
    {
        // 🧩 بيانات أساسية
        public string StudentName { get; set; }
        public string PlanTitle { get; set; }

        // 📈 الأداء الكلي
        public int TotalVideos { get; set; }
        public int CompletedVideos { get; set; }
        public int TotalWatchSeconds { get; set; }
        public double AverageQuizScore { get; set; }

        // 💡 التوصية الذكية
        public string AIAssessedLevel { get; set; }          // مستوى التحسّن (ممتاز - جيد - جزئي)
        public string AIRecommendations { get; set; }        // النص التوصيّ

        // 🔹 حساب نسبة الإنجاز
        public double CompletionPercent => TotalVideos == 0 ? 0 :
            Math.Round((CompletedVideos / (double)TotalVideos) * 100, 1);

        // 🔸 ملخص فيديوهات الجلسة (اختياري للعرض التفصيلي)
        public List<RemedialVideoReportItem> Videos { get; set; } = new();
    }

}
