using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities.Frontend;

public class CoursePackage
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;            // البكج الأول

    [Required, MaxLength(160)]
    public string Slug { get; set; } = string.Empty;            // package-1 (لاتيني فقط)

    [Required, MaxLength(100)]
    public string GradeLabel { get; set; } = string.Empty;      // للصف الثالث الثانوي

    [MaxLength(200)]
    public string? Subtitle { get; set; }                       // التأسيس + التحصيلي

    [MaxLength(1000)]
    public string? IntroText { get; set; }

    [Required, MaxLength(7)]
    public string AccentColor { get; set; } = "#14A8AE";        // #RRGGBB فقط

    public byte? DiscountPercent { get; set; }                  // 1..100

    [MaxLength(100)]
    public string? DiscountCaption { get; set; }                // على البكج الكامل

    [Required, MaxLength(60)]
    public string ButtonText { get; set; } = "احجز الآن";

    [MaxLength(500)]
    public string? PosterImagePath { get; set; }                // /uploads/packages/{guid}.webp

    public bool IsActive { get; set; } = true;
    public bool ShowOnHomePage { get; set; } = true;
    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(450)]
    public string? UpdatedByUserId { get; set; }

    public ICollection<CoursePackageItem> Items { get; set; } = new List<CoursePackageItem>();
    public ICollection<CoursePackageTag> Tags { get; set; } = new List<CoursePackageTag>();
    public ICollection<CoursePackageRegistration> Registrations { get; set; } = new List<CoursePackageRegistration>();
}
