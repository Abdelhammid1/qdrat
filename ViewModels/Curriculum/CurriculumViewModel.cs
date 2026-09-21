using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Curriculum
{
    public class CurriculumViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يجب إدخال اسم المنهج")]
        public string Title { get; set; }

        [Required(ErrorMessage = "يجب إدخال وصف المنهج")]
        public string Description { get; set; }

        [Required(ErrorMessage = "يجب اختيار الدورة التدريبية")]
        [Display(Name = "الدورة التدريبية")]
        public List<int> CourseIds { get; set; } = new List<int>(); // ✅ دعم ربط أكثر من دورة
      
        [ValidateNever]
        public List<SelectListItem> Courses { get; set; } = new List<SelectListItem>();
        [Display(Name = "هل المنهج كمي؟")]
        public bool IsQuantitative { get; set; } = false; // ✅ صح

        public bool IsRTL { get; set; } = true;
        [ValidateNever]
        public List<CurriculumModuleViewModel> Modules { get; set; }  // ✅ وحدات مرتبطة بالمنهج

        [ValidateNever]
        public string CourseName { get; set; }  // ✅ اسم الدورة للعرض فقط

        [Required(ErrorMessage = "يجب اختيار نوع المنهج")]
        [Display(Name = "نوع المنهج")]
        public string CurriculumTypeName { get; set; }  // ✅ نوع المنهج (كمي / لفظي / تحصيلي)

        [ValidateNever]
        public List<SelectListItem> CurriculumTypeOptions => new List<SelectListItem>
        {
            new SelectListItem { Value = "كمي", Text = "كمي" },
            new SelectListItem { Value = "لفظي", Text = "لفظي" },
            new SelectListItem { Value = "تحصيلي", Text = "تحصيلي / آخر" }
        };
    }
}
