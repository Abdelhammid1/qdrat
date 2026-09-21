namespace QdratNew.ViewModels.Dashboard
{
    public class BatchPerformanceDetailViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public List<SectionPerformanceItem> Sections { get; set; }
    }
}
