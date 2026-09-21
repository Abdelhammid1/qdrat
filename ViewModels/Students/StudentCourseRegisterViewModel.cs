using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Students
{
    public class StudentCourseRegisterViewModel
    {
        [Required(ErrorMessage = "يجب اختيار الطالب")]
        [Display(Name = "الطالب")]
        public int StudentID { get; set; }

        [Required(ErrorMessage = "يجب اختيار الدورة")]
        [Display(Name = "الدورة")]
        public int CourseID { get; set; }

        public List<SelectListItem> Students { get; set; } = new();
        public List<SelectListItem> Courses { get; set; } = new();
    }
}
