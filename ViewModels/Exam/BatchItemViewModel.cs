namespace QdratNew.ViewModels.Exam
{
    public class BatchItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CourseName { get; set; }
        public int StudentCount { get; set; }
    }

    public class AllBatchesForAnalysisViewModel
    {
        public List<BatchItemViewModel> Batches { get; set; }
    }

}
