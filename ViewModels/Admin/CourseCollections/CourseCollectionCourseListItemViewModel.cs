namespace QdratNew.ViewModels.Admin.CourseCollections;

public class CourseCollectionCourseListItemViewModel
{
    public int Id { get; set; }
    public int CourseCollectionId { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string? StandardCode { get; set; }
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool ShowOnHomePage { get; set; }
    public bool IsRegistrationOpen { get; set; }
    public int RegistrationCount { get; set; }
    public int DisplayOrder { get; set; }
    public string? LogoPath { get; set; }
}
