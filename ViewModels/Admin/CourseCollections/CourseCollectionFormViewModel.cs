using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using QdratNew.Enums;

namespace QdratNew.ViewModels.Admin.CourseCollections;

public class CourseCollectionFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "اسم المجموعة مطلوب")]
    [MaxLength(200)]
    [Display(Name = "اسم المجموعة")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "الـ Slug مطلوب")]
    [MaxLength(200)]
    [Display(Name = "Slug")]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(300)]
    [Display(Name = "عنوان فرعي")]
    public string? Subtitle { get; set; }

    [MaxLength(1000)]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [MaxLength(100)]
    [Display(Name = "نص الزر")]
    public string? ButtonText { get; set; }

    [Display(Name = "شكل العرض")]
    public CourseCollectionDisplayStyle DisplayStyle { get; set; } = CourseCollectionDisplayStyle.Grid;

    [Display(Name = "نشط")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "يظهر في الصفحة الرئيسية")]
    public bool ShowOnHomePage { get; set; } = true;

    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;

    [Display(Name = "إغلاق تلقائي عند اكتمال العدد")]
    public bool AutoCloseWhenMaxReached { get; set; } = false;

    [Display(Name = "الحد الأقصى الإجمالي للطلبات")]
    public int? GlobalMaxRequests { get; set; }

    [MaxLength(500)]
    [Display(Name = "رسالة الإغلاق")]
    public string? ClosedMessage { get; set; }

    // اللوجو
    public string? ExistingLogoPath { get; set; }

    [Display(Name = "لوجو المجموعة")]
    public IFormFile? LogoFile { get; set; }

    public bool RemoveLogo { get; set; } = false;

    // خلفية السكشن
    public string? ExistingBackgroundImagePath { get; set; }

    [Display(Name = "خلفية السكشن")]
    public IFormFile? BackgroundImageFile { get; set; }

    public bool RemoveBackgroundImage { get; set; } = false;
}
