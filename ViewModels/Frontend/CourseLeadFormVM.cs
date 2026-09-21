using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Frontend
{
    public class CourseLeadFormVM
    {
        [Required(ErrorMessage = "اسم الطالب مطلوب")]
        public string StudentName { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        public string PhoneNumber { get; set; }

        // ✅ اسم البرنامج كنص
        [Required(ErrorMessage = "اختر البرنامج")]
        public string SelectedProgram { get; set; }

        // للعرض فقط
        public List<SelectListItem> Programs { get; set; } = new();
    }
}
