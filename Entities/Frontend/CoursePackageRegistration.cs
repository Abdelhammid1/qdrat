using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities.Frontend;

public class CoursePackageRegistration
{
    public int Id { get; set; }

    public int CoursePackageId { get; set; }
    public CoursePackage? CoursePackage { get; set; }

    [Required, MaxLength(150)]
    public string PackageNameSnapshot { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    public CoursePackageRegistrationStatus Status { get; set; } = CoursePackageRegistrationStatus.New;

    [MaxLength(1000)]
    public string? AdminNotes { get; set; }

    public DateTime? ContactedAt { get; set; }

    [MaxLength(450)]
    public string? HandledByUserId { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAt { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }
}
