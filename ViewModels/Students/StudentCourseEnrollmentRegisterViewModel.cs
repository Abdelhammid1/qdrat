
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Students
{
    public class StudentCourseEnrollmentRegisterViewModel
    {
        [Required]
        [Display(Name = "الطالب")]
        public int StudentId { get; set; }

        [Required]
        [Display(Name = "الدورة")]
        public int CourseId { get; set; }

        public List<SelectListItem> Students { get; set; } = new();
        public List<SelectListItem> Courses { get; set; } = new();
    }
}
