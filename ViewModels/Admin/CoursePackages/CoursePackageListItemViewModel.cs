namespace QdratNew.ViewModels.Admin.CoursePackages;

public class CoursePackageListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string GradeLabel { get; set; } = string.Empty;
    public byte? DiscountPercent { get; set; }
    public int ItemsCount { get; set; }
    public int RegistrationsCount { get; set; }
    public int NewRegistrationsCount { get; set; }
    public bool IsActive { get; set; }
    public bool ShowOnHomePage { get; set; }
    public int DisplayOrder { get; set; }
    public string? PosterImagePath { get; set; }
}
