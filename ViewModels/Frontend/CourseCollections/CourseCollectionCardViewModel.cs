using QdratNew.Enums;

namespace QdratNew.ViewModels.Frontend.CourseCollections;

public class CourseCollectionCardViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string? ButtonText { get; set; }
    public string? LogoPath { get; set; }
    public string? BackgroundImagePath { get; set; }
    public CourseCollectionDisplayStyle DisplayStyle { get; set; }

    public List<CourseCollectionCourseCardViewModel> Courses { get; set; } = new();
    public List<CourseCollectionSponsorViewModel> Sponsors { get; set; } = new();
}
