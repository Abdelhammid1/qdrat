using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace QdratNew.ViewModels.Instructor.Exam
{
    public class SectionWithLessonsVM
    {
        public int SectionId { get; set; }
        public string SectionName { get; set; }
        [ValidateNever] // ✅ تجاهل التحقق لهذا الحقل

        public List<LessonGenerateVM> Lessons { get; set; } = new();
    }
}
