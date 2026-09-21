using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace QdratNew.ViewModels.Admin.CourseCollections;

public class CourseCollectionSponsorFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "المجموعة مطلوبة")]
    [Display(Name = "المجموعة")]
    public int CourseCollectionId { get; set; }

    [MaxLength(200)]
    [Display(Name = "اسم الراعي")]
    public string? Name { get; set; }

    [MaxLength(500)]
    [Display(Name = "رابط الراعي")]
    public string? LinkUrl { get; set; }

    [Display(Name = "يظهر في الصفحة الرئيسية")]
    public bool ShowOnHomePage { get; set; } = true;

    [Display(Name = "ترتيب العرض")]
    public int DisplayOrder { get; set; } = 0;

    // اللوجو
    public string? ExistingLogoPath { get; set; }

    [Display(Name = "لوجو الراعي")]
    public IFormFile? LogoFile { get; set; }

    public bool RemoveLogo { get; set; } = false;
}
