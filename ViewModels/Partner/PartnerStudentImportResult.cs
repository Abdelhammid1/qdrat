namespace QdratNew.ViewModels.Partner
{
    public class PartnerStudentImportResult
    {
        public bool Success { get; set; }
        public int InsertedCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
