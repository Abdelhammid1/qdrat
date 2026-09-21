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


        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>(); // ✅ تأكد أن النوع صحيح

    }


}


