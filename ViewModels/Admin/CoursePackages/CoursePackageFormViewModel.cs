using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using QdratNew.Helpers;

namespace QdratNew.ViewModels.Admin.CoursePackages;

public class CoursePackageFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم الباقة مطلوب"), StringLength(150)]
    [Display(Name = "اسم الباقة")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "الرابط المختصر مطلوب"), StringLength(160)]
    [Display(Name = "الرابط المختصر")]
    public string Slug { get; set; } = string.Empty;           // يُتحقق عبر PackageStyleGuard.IsValidSlug + فرادة في الخدمة

    [Required(ErrorMessage = "وصف الصف مطلوب"), StringLength(100)]
    [Display(Name = "الصف")]
    public string GradeLabel { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "العنوان الفرعي")]
    public string? Subtitle { get; set; }

    [StringLength(1000)]
    [Display(Name = "فقرة تعريفية")]
    public string? IntroText { get; set; }

    [Required(ErrorMessage = "لون الباقة مطلوب"), StringLength(7)]
    [Display(Name = "لون الباقة")]
    public string AccentColor { get; set; } = "#14A8AE";

    [Range(1, 100, ErrorMessage = "نسبة الخصم بين 1 و100")]
    [Display(Name = "نسبة الخصم")]
    public byte? DiscountPercent { get; set; }

    [StringLength(100)]
    [Display(Name = "نص الخصم")]
    public string? DiscountCaption { get; set; }

    [Required(ErrorMessage = "نص الزر مطلوب"), StringLength(60)]
    [Display(Name = "نص زر الحجز")]
    public string ButtonText { get; set; } = "احجز الآن";

    public bool IsActive { get; set; } = true;
    public bool ShowOnHomePage { get; set; } = true;
    public int DisplayOrder { get; set; }

    // الصورة (البوستر)
    public IFormFile? PosterFile { get; set; }
    public string? ExistingPosterPath { get; set; }
    public bool RemovePoster { get; set; }

    public List<PackageItemFormViewModel> Items { get; set; } = new();
    public List<PackageTagFormViewModel> Tags { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!PackageStyleGuard.IsValidHex(AccentColor)) yield return new("لون غير صالح (#RRGGBB)", new[] { nameof(AccentColor) });
        if (!PackageStyleGuard.IsValidSlug(Slug)) yield return new("الرابط: حروف إنجليزية صغيرة وأرقام وشرطات فقط", new[] { nameof(Slug) });
        if (Items.Count is < 1 or > 8) yield return new("عدد الدورات المشمولة بين 1 و8", new[] { nameof(Items) });
        if (Tags.Count > 6) yield return new("الحد الأقصى 6 وسوم", new[] { nameof(Tags) });
        if (PosterFile != null)
        {
            var ext = Path.GetExtension(PosterFile.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) yield return new("الصورة: JPG أو PNG أو WebP فقط", new[] { nameof(PosterFile) });
            if (PosterFile.Length > 5 * 1024 * 1024) yield return new("حجم الصورة لا يتجاوز 5 ميجابايت", new[] { nameof(PosterFile) });
        }
    }
}

public class PackageItemFormViewModel
{
    public int Id { get; set; }                                   // 0 = جديد

    [Required(ErrorMessage = "عنوان الدورة مطلوب"), StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string IconKey { get; set; } = "book";                 // يُتحقق عبر PackageIconCatalog

    [StringLength(7)]
    public string? ColorHex { get; set; }                         // اختياري؛ إن وُجد يُتحقق IsValidHex

    [StringLength(200)] public string? ScheduleTitle { get; set; }
    [StringLength(200)] public string? ScheduleText { get; set; }
    [StringLength(100)] public string? ScheduleNote { get; set; }
    public int DisplayOrder { get; set; }
}

public class PackageTagFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نص الوسم مطلوب"), StringLength(100)]
    public string Text { get; set; } = string.Empty;

    [StringLength(40)]
    public string? IconKey { get; set; }

    public int DisplayOrder { get; set; }
}
