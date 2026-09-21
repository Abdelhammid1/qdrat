namespace QdratNew.ViewModels.Frontend.ProfessionalCertificates;

public class ProfessionalCertificateMultiThanksViewModel
{
    public string FullName { get; set; } = string.Empty;
    public List<string> RegisteredCourseNames { get; set; } = new();
    public DateTime SubmittedAt { get; set; }
}
