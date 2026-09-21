namespace QdratNew.ViewModels.Frontend.CourseCollections;

public class CourseCollectionMultiThanksViewModel
{
    public string CollectionSlug { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<string> RegisteredCourseNames { get; set; } = new();
    public DateTime SubmittedAt { get; set; }
}
