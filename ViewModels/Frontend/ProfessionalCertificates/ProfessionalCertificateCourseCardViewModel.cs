namespace QdratNew.ViewModels.Frontend.ProfessionalCertificates;

public class ProfessionalCertificateCourseCardViewModel
{
    public int Id { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string StandardCode { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? IconClass { get; set; }
    public string? BadgeText { get; set; }
    public bool IsRegistrationOpen { get; set; }
    public bool IsMaxReached { get; set; }
    public int ActiveRegistrationCount { get; set; }
    public int? MaxRequests { get; set; }
    public string? LogoPath { get; set; }
}
