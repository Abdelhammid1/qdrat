using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities.Frontend;

public class CourseCollectionRegistration
{
    public int Id { get; set; }

    public int CourseCollectionCourseId { get; set; }
    public CourseCollectionCourse CourseCollectionCourse { get; set; } = null!;

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string NationalId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(150)]
    public string? City { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public CourseCollectionRegistrationStatus Status { get; set; } = CourseCollectionRegistrationStatus.New;

    [MaxLength(1000)]
    public string? AdminNotes { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public bool IsContacted { get; set; } = false;
    public DateTime? ContactedAt { get; set; }
    public DateTime? FollowUpAt { get; set; }

    [MaxLength(450)]
    public string? HandledByUserId { get; set; }

    [MaxLength(200)]
    public string? HandledByName { get; set; }

    [MaxLength(100)]
    public string? RegistrationBatchId { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.Now;
    public DateTime? LastUpdatedAt { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }
}
