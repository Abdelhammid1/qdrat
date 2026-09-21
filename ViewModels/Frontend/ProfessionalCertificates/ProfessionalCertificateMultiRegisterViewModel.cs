using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Frontend.ProfessionalCertificates;

public class ProfessionalCertificateMultiRegisterViewModel
{
    public List<ProfessionalCertificateCourseSelectItem> Courses { get; set; } = new();

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

    public List<int> SelectedCourseIds { get; set; } = new();
}

public class ProfessionalCertificateCourseSelectItem
{
    public int Id { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string StandardCode { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string? IconClass { get; set; }
    public string? BadgeText { get; set; }
    public string? ShortDescription { get; set; }
    public bool IsRegistrationOpen { get; set; }
    public bool IsMaxReached { get; set; }
    public bool IsAvailable => IsRegistrationOpen && !IsMaxReached;
}
