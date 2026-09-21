namespace QdratNew.ViewModels.Frontend.CourseCollections;

public class CourseCollectionThanksViewModel
{
    public int RegistrationId { get; set; }
    public string CollectionSlug { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public string CourseTitleAr { get; set; } = string.Empty;
    public string? StandardCode { get; set; }
    public DateTime SubmittedAt { get; set; }
}
