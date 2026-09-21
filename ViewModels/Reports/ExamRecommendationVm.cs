namespace QdratNew.ViewModels.Reports
{
    public class ExamRecommendationVm
    { // 🧭 المسار المقترح
        public string TrackTitle { get; set; } = string.Empty;
        public string TrackNote { get; set; } = string.Empty;

        // 🎓 بطاقتان للمسار التفصيلي
        public string TrackCard1 { get; set; } = string.Empty;
        public string TrackCard1Desc { get; set; } = string.Empty;
        public string TrackCard2 { get; set; } = string.Empty;
        public string TrackCard2Desc { get; set; } = string.Empty;
        public double TotalMinutes { get; set; }
        public string PerformanceSummary { get; set; } = string.Empty;

        // ⚡ السرعة والتركيز
        public string SpeedLabel { get; set; } = string.Empty;
        public string SpeedNote { get; set; } = string.Empty;
        // 🧠 التوصيات الفردية (المفقودة)
        public List<string> IndividualTips { get; set; } = new List<string>();

    }
}
