using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels
{
    public class StudentFullViewModel
    {
        public int StudentID { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        public string FullName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "الجنس مطلوب")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "اسم المدرسة مطلوب")]
        public string School { get; set; }

        [Required(ErrorMessage = "المرحلة التعليمية مطلوبة")]
        public string Level { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        public int BatchId { get; set; }

        public int? ParentId { get; set; }

        public string EnrollmentStatus { get; set; } = "نشط";

        public List<SelectListItem> Branches { get; set; }
        public List<SelectListItem> Batches { get; set; }
        public List<SelectListItem> Parents { get; set; }
    }
}
