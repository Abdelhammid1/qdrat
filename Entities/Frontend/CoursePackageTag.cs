using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities.Frontend;

public class CoursePackageTag
{
    public int Id { get; set; }
    public int CoursePackageId { get; set; }
    public CoursePackage? CoursePackage { get; set; }

    [Required, MaxLength(100)]
    public string Text { get; set; } = string.Empty;            // 7 أيام كمي

    [MaxLength(40)]
    public string? IconKey { get; set; }

    public int DisplayOrder { get; set; }
}
