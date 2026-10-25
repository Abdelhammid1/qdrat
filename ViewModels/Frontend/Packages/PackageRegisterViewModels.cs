using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Frontend.Packages;

/// <summary>نموذج الحجز العام: الاسم + الجوال فقط (الباقة تُحدَّد بالـ slug من المسار).</summary>
public class PackageRegisterFormViewModel
{
    [Required(ErrorMessage = "الاسم مطلوب")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "الاسم بين 3 و150 حرفًا")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "رقم الجوال مطلوب")]
    [RegularExpression(@"^05\d{8}$", ErrorMessage = "رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام")]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Honeypot — حقل مخفي يجب أن يبقى فارغًا.</summary>
    public string? Website { get; set; }
}

/// <summary>نموذج صفحة التفاصيل: بيانات الباقة + نموذج الحجز + حالة الإرسال.</summary>
public class PackageDetailsPageViewModel
{
    public PackageDetailsViewModel Package { get; set; } = new();
    public PackageRegisterFormViewModel Form { get; set; } = new();
    public bool Submitted { get; set; }
}

public class SubmitPackageResult
{
    public bool Success { get; set; }
    public bool NotFound { get; set; }
    public Dictionary<string, string> Errors { get; set; } = new();
}
