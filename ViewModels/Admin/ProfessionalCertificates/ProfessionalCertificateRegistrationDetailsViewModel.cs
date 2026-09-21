using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.ProfessionalCertificates;

public class ProfessionalCertificateRegistrationDetailsViewModel
{
    public int Id { get; set; }
    public string? RegistrationBatchId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? City { get; set; }
    public string? Notes { get; set; }
    public ProfessionalCertificateRegistrationStatus Status { get; set; }
    public string? AdminNotes { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsContacted { get; set; }
    public DateTime? ContactedAt { get; set; }
    public DateTime? FollowUpAt { get; set; }
    public string? HandledByName { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // All courses in this submission
    public List<PcrCourseBadge> AllCourses { get; set; } = new();

    // Primary course (kept for backward compat with single-course regs)
    public int CourseId { get; set; }
    public string CourseTitleAr { get; set; } = string.Empty;
    public string StandardCode { get; set; } = string.Empty;
}
