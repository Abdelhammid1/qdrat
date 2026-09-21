using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    public class PlacementExamCreateViewModel
    {
        [Required(ErrorMessage = "اختيار الطالب مطلوب")]
        [Display(Name = "الطالب")]
        public int? StudentId { get; set; }

        [Required(ErrorMessage = "اختيار الدورة مطلوب")]
        [Display(Name = "الدورة")]
        public int? CourseId { get; set; }

        // ✅ لعرض الطلاب في DropDown
        [ValidateNever]
        public List<SelectListItem> Students { get; set; } = new List<SelectListItem>();

        // ✅ لعرض الدورات في DropDown
        [ValidateNever]
        public List<SelectListItem> Courses { get; set; } = new List<SelectListItem>();
    }
}
