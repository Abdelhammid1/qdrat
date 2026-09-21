using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace QdratNew.ViewModels.Admin.ProfessionalCertificates;

public class ProfessionalCertificateCourseFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم الدورة مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم الدورة")]
    public string TitleAr { get; set; } = string.Empty;

    [Required(ErrorMessage = "الكود الدولي مطلوب")]
    [MaxLength(100)]
    [Display(Name = "الكود الدولي")]
    public string StandardCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "الـ Slug مطلوب")]
    [MaxLength(200)]
    [Display(Name = "Slug")]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "وصف مختصر")]
    public string? ShortDescription { get; set; }

    [Display(Name = "وصف تفصيلي")]
    public string? FullDescription { get; set; }

    [MaxLength(100)]
    [Display(Name = "كلاس الأيقونة")]
    public string? IconClass { get; set; }

    [MaxLength(100)]
    [Display(Name = "نص الـ Badge")]
    public string? BadgeText { get; set; }

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "يظهر في الصفحة الرئيسية")]
    public bool ShowOnHomePage { get; set; } = true;

    [Display(Name = "التسجيل مفتوح")]
    public bool IsRegistrationOpen { get; set; } = true;

    [Display(Name = "الحد الأقصى للطلبات")]
    public int? MaxRequests { get; set; }

    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;

    // اللوجو
    public string? ExistingLogoPath { get; set; }

    [Display(Name = "لوجو الدورة")]
    public IFormFile? LogoFile { get; set; }

    public bool RemoveLogo { get; set; } = false;
}
