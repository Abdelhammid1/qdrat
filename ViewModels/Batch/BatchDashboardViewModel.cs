namespace QdratNew.ViewModels.Batch
{
    public class BatchDashboardViewModel
    {
        public List<BatchAnalysisViewModel> Batches { get; set; }
        public int TotalBatches { get; set; }
        public int RecentBatches { get; set; }
        public int TotalStudents { get; set; }
        public int MaleStudents { get; set; }
        public int FemaleStudents { get; set; }
        public int MixedStudents { get; set; }
        public double TopAverageScore { get; set; }
        public string TopBatchName { get; set; }
        public double MinPassRate { get; set; }
        public int WeakBatchesCount { get; set; }
    }
}
