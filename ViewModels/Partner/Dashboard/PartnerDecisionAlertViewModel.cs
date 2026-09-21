namespace QdratNew.ViewModels.Partner.Dashboard
{
    public class PartnerDecisionAlertViewModel
    {
        public string Severity { get; set; } = string.Empty; // Green / Yellow / Orange / Red
        public string Message { get; set; } = string.Empty;

        public int? RelatedBatchId { get; set; }
        public int? RelatedInstructorId { get; set; }
    }
}
