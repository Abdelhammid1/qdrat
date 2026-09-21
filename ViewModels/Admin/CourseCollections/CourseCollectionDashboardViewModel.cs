namespace QdratNew.ViewModels.Admin.CourseCollections;

public class CourseCollectionDashboardViewModel
{
    public int TotalCount { get; set; }
    public int NewCount { get; set; }
    public int ContactedCount { get; set; }
    public int NeedFollowUpCount { get; set; }
    public int RejectedCount { get; set; }
    public int ConvertedCount { get; set; }

    public List<CourseCollectionRegistrationListItemViewModel> Items { get; set; } = new();
    public CourseCollectionRegistrationFilterViewModel Filter { get; set; } = new();
    public List<CourseCollectionListItemViewModel> CollectionsForFilter { get; set; } = new();
    public List<CourseCollectionCourseListItemViewModel> CoursesForFilter { get; set; } = new();

    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
