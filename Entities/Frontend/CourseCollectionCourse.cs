using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities.Frontend;

public class CourseCollectionCourse
{
    public int Id { get; set; }

    public int CourseCollectionId { get; set; }
    public CourseCollection CourseCollection { get; set; } = null!;

    [Required, MaxLength(200)]
    public string TitleAr { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? StandardCode { get; set; }

    [Required, MaxLength(200)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ShortDescription { get; set; }

    public string? FullDescription { get; set; }

    [MaxLength(100)]
    public string? IconClass { get; set; }

    [MaxLength(100)]
    public string? BadgeText { get; set; }

    [MaxLength(500)]
    public string? LogoPath { get; set; }

    public bool IsActive { get; set; } = true;
    public bool ShowOnHomePage { get; set; } = true;
    public bool IsRegistrationOpen { get; set; } = true;
    public int? MaxRequests { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<CourseCollectionRegistration> Registrations { get; set; } = new List<CourseCollectionRegistration>();
}
