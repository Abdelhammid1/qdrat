namespace QdratNew.ViewModels.Admin.ProfessionalCertificates;

public class ProfessionalCertificateDashboardViewModel
{
    public int TotalCount { get; set; }
    public int NewCount { get; set; }
    public int ContactedCount { get; set; }
    public int NeedFollowUpCount { get; set; }
    public int RejectedCount { get; set; }
    public int ConvertedCount { get; set; }

    public List<ProfessionalCertificateRegistrationListItemViewModel> Items { get; set; } = new();
    public ProfessionalCertificateRegistrationFilterViewModel Filter { get; set; } = new();
    public List<ProfessionalCertificateCourseListItemViewModel> CoursesForFilter { get; set; } = new();

    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
