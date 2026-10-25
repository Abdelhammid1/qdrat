using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities.Frontend;

public class CoursePackageItem
{
    public int Id { get; set; }
    public int CoursePackageId { get; set; }
    public CoursePackage? CoursePackage { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;           // دورة القدرات التأسيسية

    [Required, MaxLength(40)]
    public string IconKey { get; set; } = "book";

    [MaxLength(7)]
    public string? ColorHex { get; set; }                       // اختياري، #RRGGBB

    [MaxLength(200)]
    public string? ScheduleTitle { get; set; }                  // إن اختلف عن Title في الجدول

    [MaxLength(200)]
    public string? ScheduleText { get; set; }                   // من 8 نوفمبر إلى 3 ديسمبر 2026م.

    [MaxLength(100)]
    public string? ScheduleNote { get; set; }                   // (عبر منصة المعهد)

    public int DisplayOrder { get; set; }
}
