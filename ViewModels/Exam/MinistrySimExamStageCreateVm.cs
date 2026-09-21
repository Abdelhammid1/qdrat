using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    // شاشة بناء اختبار معمل القياس (Sprint 2 — MSE-B): 5 كروت مراحل + Dropdown محور لكل منهج
    public class MinistrySimExamCreateVm
    {
        [Required(ErrorMessage = "يرجى اختيار الدورة")]
        public int CourseId { get; set; }

        [ValidateNever]
        public string CourseName { get; set; }

        [Required(ErrorMessage = "يرجى إدخال عنوان الاختبار")]
        public string Title { get; set; }

        // ✅ قوائم المحاور (Section) الخاصة بالدورة المختارة — كمي/لفظي، مشتركة بين كل الكروت الخمسة
        [ValidateNever]
        public List<SelectListItem> QuantSections { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> VerbalSections { get; set; } = new();

        public List<MinistrySimExamStageCreateVm> Stages { get; set; } = new();
    }

    // كرت مرحلة واحدة من المراحل الخمس
    public class MinistrySimExamStageCreateVm
    {
        public int StageNumber { get; set; } // 1..5

        [Required(ErrorMessage = "يرجى اختيار المحور الكمي")]
        [Display(Name = "المحور الكمي")]
        public int QuantSectionId { get; set; }

        [Range(1, 200, ErrorMessage = "عدد أسئلة المحور الكمي غير صالح")]
        [Display(Name = "عدد أسئلة المحور الكمي")]
        public int QuantQuestionCount { get; set; } = 11;

        [Required(ErrorMessage = "يرجى اختيار المحور اللفظي")]
        [Display(Name = "المحور اللفظي")]
        public int VerbalSectionId { get; set; }

        [Range(1, 200, ErrorMessage = "عدد أسئلة المحور اللفظي غير صالح")]
        [Display(Name = "عدد أسئلة المحور اللفظي")]
        public int VerbalQuestionCount { get; set; } = 13;

        [Range(1, 180, ErrorMessage = "مدة المرحلة غير صالحة")]
        [Display(Name = "مدة المرحلة (دقيقة)")]
        public int DurationMinutes { get; set; } = 26;

        // Sprint 5 (MSE-C / C4): اختيارات المؤشرات (Lesson/Difficulty/العدد) المُرسَلة من قائمة المؤشرات الحية لكل محور
        public List<MinistrySimExamStageIndicatorInputVm> QuantIndicators { get; set; } = new();
        public List<MinistrySimExamStageIndicatorInputVm> VerbalIndicators { get; set; } = new();
    }

    // Sprint 5 (MSE-C / C4): مدخل مؤشر واحد (LessonId + Difficulty + العدد المطلوب) قادم من نموذج البناء
    public class MinistrySimExamStageIndicatorInputVm
    {
        public int LessonId { get; set; }
        public DifficultyLevel Difficulty { get; set; }
        public int RequestedCount { get; set; }
    }
}
