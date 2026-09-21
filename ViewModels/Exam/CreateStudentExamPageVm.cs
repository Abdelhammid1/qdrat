using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Exam
{
    public class CreateStudentExamPageVm
    {
        public CreateStudentExamVm Input { get; set; } = new CreateStudentExamVm();
        [ValidateNever]
        public List<SelectListItem> Students { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> Curriculums { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> ProfessionalModels { get; set; } = new();
    }
}
