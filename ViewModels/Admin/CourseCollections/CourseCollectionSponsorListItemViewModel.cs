namespace QdratNew.ViewModels.Admin.CourseCollections;

public class CourseCollectionSponsorListItemViewModel
{
    public int Id { get; set; }
    public int CourseCollectionId { get; set; }
    public string? Name { get; set; }
    public string LogoPath { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool ShowOnHomePage { get; set; }
    public int DisplayOrder { get; set; }
}
