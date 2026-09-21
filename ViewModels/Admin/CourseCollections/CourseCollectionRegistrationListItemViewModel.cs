using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.CourseCollections;

public class CourseCollectionRegistrationListItemViewModel
{
    public int Id { get; set; }
    public string? RegistrationBatchId { get; set; }
    public string CollectionName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public List<CcrCourseBadge> Courses { get; set; } = new();
    public CourseCollectionRegistrationStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string? HandledByName { get; set; }
}

public class CcrCourseBadge
{
    public int RegistrationId { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string? StandardCode { get; set; }
}
