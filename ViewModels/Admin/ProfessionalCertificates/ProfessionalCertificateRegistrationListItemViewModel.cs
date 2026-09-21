using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.ProfessionalCertificates;

public class ProfessionalCertificateRegistrationListItemViewModel
{
    public int Id { get; set; }
    public string? RegistrationBatchId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public List<PcrCourseBadge> Courses { get; set; } = new();
    public ProfessionalCertificateRegistrationStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string? HandledByName { get; set; }
}

public class PcrCourseBadge
{
    public int RegistrationId { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string StandardCode { get; set; } = string.Empty;
}
