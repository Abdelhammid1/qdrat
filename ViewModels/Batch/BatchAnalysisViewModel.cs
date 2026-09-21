namespace QdratNew.ViewModels.Batch
{
    public class BatchAnalysisViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string CourseName { get; set; }

        public int StudentCount { get; set; }
        public double AverageScore { get; set; }
        public double PassRate { get; set; }
        public string PerformanceLevel { get; set; }
        public string BranchName { get; set; } // ✅ اسم الفرع

        public string Gender { get; set; } // أو Enum لو تحب
    }
}
