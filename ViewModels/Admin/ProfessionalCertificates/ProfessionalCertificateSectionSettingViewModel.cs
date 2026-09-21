using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Admin.ProfessionalCertificates;

public class ProfessionalCertificateSectionSettingViewModel
{
    public int Id { get; set; } = 1;

    [Display(Name = "تفعيل القسم")]
    public bool IsEnabled { get; set; } = true;

    [Required(ErrorMessage = "العنوان مطلوب")]
    [MaxLength(200)]
    [Display(Name = "عنوان القسم")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(300)]
    [Display(Name = "العنوان الفرعي")]
    public string? Subtitle { get; set; }

    [MaxLength(1000)]
    [Display(Name = "وصف القسم")]
    public string? Description { get; set; }

    [MaxLength(100)]
    [Display(Name = "نص الزر")]
    public string? ButtonText { get; set; }

    [Display(Name = "إغلاق تلقائي عند اكتمال العدد")]
    public bool AutoCloseWhenMaxReached { get; set; }

    [Display(Name = "الحد الأقصى الإجمالي للطلبات")]
    public int? GlobalMaxRequests { get; set; }

    [MaxLength(500)]
    [Display(Name = "رسالة الإغلاق")]
    public string? ClosedMessage { get; set; }
}
