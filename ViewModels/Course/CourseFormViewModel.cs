using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Course
{
    public class CourseFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الدورة مطلوب")]
        public string Name { get; set; }

        [Required(ErrorMessage = "الوصف مطلوب")]
        public string Description { get; set; }

        [Required(ErrorMessage = "تاريخ البدء مطلوب")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "تاريخ الانتهاء مطلوب")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "يجب اختيار فرع")]
        public int? BranchId { get; set; } // ✅ هو المطلوب وليس `BranchName`

        public string? BranchName { get; set; } // ❌ إزالة `[Required]` لأنه ليس مدخلًا من المستخدم

        [Required(ErrorMessage = "يجب اختيار مشروع")]
        public int? ProjectId { get; set; } // ✅ هو المطلوب وليس `ProjectName`

        public string? ProjectName { get; set; } // ❌ إزالة `[Required]` لأنه ليس مدخلًا من المستخدم
        public bool IsActive { get; set; }

        // ===== الظهور في صفحة التسجيل العامة (RL) =====
        [Display(Name = "يظهر في صفحة التسجيل")]
        public bool ShowOnRegisterPage { get; set; }

        [Display(Name = "ترتيب العرض")]
        [Range(0, 9999, ErrorMessage = "الترتيب بين 0 و 9999")]
        public int RegisterDisplayOrder { get; set; }

        [Display(Name = "الوصف العام")]
        [MaxLength(300, ErrorMessage = "الوصف العام لا يزيد عن 300 حرف")]
        public string? PublicDescription { get; set; }

        // للعرض فقط (تنبيه: الدورة لن تظهر إذا كان برنامجها مخفيًا)
        public bool? ProjectShowOnRegisterPage { get; set; }
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Projects { get; set; } = new List<SelectListItem>();
    }


}
