using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities.Frontend;

public class CourseCollection
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Subtitle { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ButtonText { get; set; }

    [MaxLength(500)]
    public string? LogoPath { get; set; }

    [MaxLength(500)]
    public string? BackgroundImagePath { get; set; }

    public CourseCollectionDisplayStyle DisplayStyle { get; set; } = CourseCollectionDisplayStyle.Grid;

    public bool IsActive { get; set; } = true;
    public bool ShowOnHomePage { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;

    public bool AutoCloseWhenMaxReached { get; set; } = false;
    public int? GlobalMaxRequests { get; set; }

    [MaxLength(500)]
    public string? ClosedMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(450)]
    public string? UpdatedByUserId { get; set; }

    public ICollection<CourseCollectionCourse> Courses { get; set; } = new List<CourseCollectionCourse>();
    public ICollection<CourseCollectionSponsor> Sponsors { get; set; } = new List<CourseCollectionSponsor>();
}
