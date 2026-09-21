using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities.Frontend;

public class CourseCollectionSponsor
{
    public int Id { get; set; }

    public int CourseCollectionId { get; set; }
    public CourseCollection CourseCollection { get; set; } = null!;

    [MaxLength(200)]
    public string? Name { get; set; }

    [Required, MaxLength(500)]
    public string LogoPath { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? LinkUrl { get; set; }

    public bool ShowOnHomePage { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}
