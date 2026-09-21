using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.ProfessionalCertificates;

public class ProfessionalCertificateRegistrationUpdateStatusViewModel
{
    public int RegistrationId { get; set; }
    public ProfessionalCertificateRegistrationStatus NewStatus { get; set; }
    public string? AdminNotes { get; set; }
    public string? RejectionReason { get; set; }
}
