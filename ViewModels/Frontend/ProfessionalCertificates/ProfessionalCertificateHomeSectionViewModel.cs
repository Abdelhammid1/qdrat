namespace QdratNew.ViewModels.Frontend.ProfessionalCertificates;

public class ProfessionalCertificateHomeSectionViewModel
{
    public bool IsEnabled { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string? ButtonText { get; set; }
    public List<ProfessionalCertificateCourseCardViewModel> Courses { get; set; } = new();
}
