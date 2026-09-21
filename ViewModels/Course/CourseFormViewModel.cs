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
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Projects { get; set; } = new List<SelectListItem>();
    }


}
