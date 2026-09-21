using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using QdratNew.ViewModels.Instructor.Exam;

namespace QdratNew.ViewModels.Instructor.Exam
{
    public class ExamAutoGenerateVM
    {
        public int CurriculumId { get; set; }

        public int EasyCount { get; set; }
        public int MediumCount { get; set; }
        public int HardCount { get; set; }
        [ValidateNever] // ✅ تجاهل التحقق لهذا الحقل

        public List<int> LessonIds { get; set; } = new();
        [ValidateNever] // ✅ تجاهل التحقق لهذا الحقل

        public List<SelectItemVM> Lessons { get; set; } = new();




        public string Title { get; set; } // 🔥 عنوان النموذج

        public List<SectionWithLessonsVM> Sections { get; set; } = new();

    }
}
