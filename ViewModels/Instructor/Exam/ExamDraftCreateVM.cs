using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using QdratNew.ViewModels.Instructor.Exam;

namespace QdratNew.ViewModels.Instructor.Exam
{
    public class ExamDraftCreateVM
    {
        [ValidateNever] // ✅ تجاهل التحقق لهذا الحقل

        public List<SelectItemVM> Curriculums { get; set; } = new();
    }
}
