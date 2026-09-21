using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.CourseCollections;

public class CourseCollectionListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public CourseCollectionDisplayStyle DisplayStyle { get; set; }
    public int CourseCount { get; set; }
    public int SponsorCount { get; set; }
    public bool IsActive { get; set; }
    public bool ShowOnHomePage { get; set; }
    public int DisplayOrder { get; set; }
}
