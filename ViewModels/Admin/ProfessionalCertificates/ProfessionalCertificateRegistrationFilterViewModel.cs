using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.ProfessionalCertificates;

public class ProfessionalCertificateRegistrationFilterViewModel
{
    public string? SearchText { get; set; }
    public int? CourseId { get; set; }
    public ProfessionalCertificateRegistrationStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
