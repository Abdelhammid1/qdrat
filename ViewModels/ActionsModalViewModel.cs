// ViewModels/ActionsModalViewModel.cs
namespace QdratNew.ViewModels
{
    public class ActionsModalViewModel
    {
        public Guid ItemId { get; set; }
        public string? ItemTitle { get; set; }

        public string? EditUrl { get; set; }
        public string? DetailsUrl { get; set; }

        public bool EnableDelete { get; set; } = true;
        public bool EnableLabels { get; set; } = false;
        public string? LabelsModalId { get; set; } // مثل: "labelsModal_@q.Id"

        public bool EnableAudit { get; set; } = false;
        public bool EnableSimilarityCheck { get; set; } = false;

        public string? AuditModalId { get; set; }

    }
}
