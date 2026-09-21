using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;

namespace QdratNew.ViewModels.Curriculum
{
    public class CurriculumInstructorViewModel
    {
        public int Id { get; set; }

        public int CurriculumId { get; set; }

        [Display(Name = "المنهج")]
        public IEnumerable<SelectListItem>? CurriculumList { get; set; }

        public int InstructorId { get; set; }

        [Display(Name = "المدرب")]
        public IEnumerable<SelectListItem>? InstructorList { get; set; }

        [Required]
        [Display(Name = "الجنس")]
        public GenderType Gender { get; set; }

        [Display(Name = "تاريخ الإسناد")]
        public DateTime AssignedDate { get; set; } = DateTime.Now;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }
        public bool ReturnToInstructors { get; set; } = false;
        public string? CurriculumTitle { get; set; }
        public string? InstructorName { get; set; }


    }
}
