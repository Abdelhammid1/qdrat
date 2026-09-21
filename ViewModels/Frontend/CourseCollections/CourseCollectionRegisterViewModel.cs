using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Frontend.CourseCollections;

public class CourseCollectionRegisterViewModel
{
    public string CollectionSlug { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;

    public int CourseId { get; set; }
    public string CourseTitleAr { get; set; } = string.Empty;
    public string? StandardCode { get; set; }
    public string? CourseShortDescription { get; set; }
    public string? CourseFullDescription { get; set; }
    public string? LogoPath { get; set; }

    [Required(ErrorMessage = "الاسم الكامل مطلوب")]
    [MaxLength(200)]
    [Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "رقم الجوال مطلوب")]
    [MaxLength(20)]
    [Display(Name = "رقم الجوال")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "رقم الهوية مطلوب")]
    [MaxLength(20)]
    [Display(Name = "رقم الهوية الوطنية")]
    public string NationalId { get; set; } = string.Empty;

    [MaxLength(200)]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
    [Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    [MaxLength(150)]
    [Display(Name = "المدينة")]
    public string? City { get; set; }

    [MaxLength(1000)]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }
}
