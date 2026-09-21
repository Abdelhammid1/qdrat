using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Instructor
{
    public class InstructorCurriculumBatchViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يجب اختيار المدرب")]
        public int InstructorId { get; set; }

        [Required(ErrorMessage = "يجب اختيار المنهج")]
        public int CurriculumId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الدفعة")]
        public int BatchId { get; set; }

        // ✅ تجاهل التحقق للفورم لأن هذه قوائم اختيارية فقط
        [ValidateNever]
        public IEnumerable<SelectListItem> Instructors { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem> Curriculums { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem> Batches { get; set; }

        // ✅ أسماء العرض فقط، لا تدخل ضمن التحقق
        [ValidateNever]
        public string InstructorName { get; set; }

        [ValidateNever]
        public string CurriculumName { get; set; }

        [ValidateNever]
        public string BatchName { get; set; }

        [ValidateNever]
        public string BatchGenderName { get; set; }

    }
}
