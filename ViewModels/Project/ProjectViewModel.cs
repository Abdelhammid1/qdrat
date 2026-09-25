using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace QdratNew.ViewModels.Project
{

    public class ProjectViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المشروع مطلوب")]
        public string Name { get; set; }

        [Required(ErrorMessage = "الوصف مطلوب")]
        public string Description { get; set; }

        [Required(ErrorMessage = "تاريخ البداية مطلوب")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }  // ✅ تأكد من أن StartDate معرف هنا
        public DateTime? EndDate { get; set; }  // ✅ تأكد من أن StartDate معرف هنا
        public bool IsActive { get; set; }


        [Required(ErrorMessage = "يجب اختيار فرع")]
        public int BranchId { get; set; } // ✅ هذا هو الـ ID المرتبط بالفرع

        [Display(Name = "التاريخ المتوقع للانتهاء")]
        [DataType(DataType.Date)]
        public DateTime? ExpectedEndDate { get; set; }


        // ===== الظهور في صفحة التسجيل العامة (RL) =====
        [Display(Name = "يظهر في صفحة التسجيل")]
        public bool ShowOnRegisterPage { get; set; }

        [Display(Name = "ترتيب العرض")]
        [Range(0, 9999, ErrorMessage = "الترتيب بين 0 و 9999")]
        public int RegisterDisplayOrder { get; set; }

        [Display(Name = "الوصف العام")]
        [MaxLength(500, ErrorMessage = "الوصف العام لا يزيد عن 500 حرف")]
        public string? PublicDescription { get; set; }

        [Display(Name = "الأيقونة")]
        [MaxLength(50, ErrorMessage = "اسم الأيقونة لا يزيد عن 50 حرفًا")]
        [RegularExpression(@"^[A-Za-z0-9\- ]*$", ErrorMessage = "اسم الأيقونة أحرف إنجليزية وأرقام وشرطات فقط")]
        public string? RegisterIcon { get; set; }

        [Display(Name = "اللون")]
        [RegularExpression(@"^(#[0-9A-Fa-f]{6})?$", ErrorMessage = "اللون بصيغة #RRGGBB")]
        public string? RegisterAccentColor { get; set; }

        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>(); // ✅ تأكد أن النوع صحيح

    }


}


