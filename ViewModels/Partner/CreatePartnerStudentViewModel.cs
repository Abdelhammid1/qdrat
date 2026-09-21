using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Partner
{
    public class CreatePartnerStudentViewModel
    {

        public string FullName { get; set; }
        public string NationalId { get; set; }
        public string Phone { get; set; }

        public int CourseId { get; set; }
        public int BatchId { get; set; }
        [ValidateNever]
        public List<SelectListItem> Courses { get; set; }
        [ValidateNever]
        public List<SelectListItem> Batches { get; set; }


        [Required(ErrorMessage = "الجنس مطلوب")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "المرحلة التعليمية مطلوبة")]
        public string Level { get; set; }

        public int BranchId { get; set; }


        // =========================
        // 🔹 للـ UI فقط
        // =========================

        [ValidateNever]
        public List<SelectListItem> Branches { get; set; }
      
    }


}
