using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.CourseCollections;

public class CourseCollectionRegistrationUpdateStatusViewModel
{
    public int RegistrationId { get; set; }
    public CourseCollectionRegistrationStatus NewStatus { get; set; }
    public string? AdminNotes { get; set; }
    public string? RejectionReason { get; set; }
}
