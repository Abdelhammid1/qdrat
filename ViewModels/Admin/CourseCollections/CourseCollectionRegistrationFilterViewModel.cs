using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.CourseCollections;

public class CourseCollectionRegistrationFilterViewModel
{
    public string? SearchText { get; set; }
    public int? CourseCollectionId { get; set; }
    public int? CourseId { get; set; }
    public CourseCollectionRegistrationStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
