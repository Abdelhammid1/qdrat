using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    public enum ExamGenerationMode
    {
        AutoFromBank = 1,   // توليد من بنك الأسئلة
        FromQuestionPools = 2 // توليد من مخازن الأسئلة (نموذج سابق)
    }

    public class UnifiedExamCreationViewModel
    {
        [Required(ErrorMessage = "عنوان الاختبار مطلوب")]
        public string Title { get; set; }

        [Required(ErrorMessage = "يجب اختيار دورة")]
        public int CourseId { get; set; }

        [Required(ErrorMessage = "يجب اختيار دفعة واحدة على الأقل")]
        public List<int> SelectedBatchIds { get; set; } = new();

        [Required(ErrorMessage = "الرجاء تحديد نوع التوليد")]
        public ExamGenerationMode GenerationMode { get; set; }

        // 🧩 في حالة FromQuestionPools
        public List<int> SelectedQuestionPoolIds { get; set; } = new();

        [Range(5, 200, ErrorMessage = "عدد الأسئلة يجب أن يكون بين 5 و 200")]
        public int QuestionCount { get; set; } = 40;

        [Required]
        public ExamType ExamType { get; set; } = ExamType.Course;

        [Required]
        [Range(5, 180, ErrorMessage = "المدة يجب أن تكون بين 5 و 180 دقيقة")]
        public int DurationMinutes { get; set; } = 30;

        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        // 🔹 حضوري أم أونلاين
        public bool IsInLab { get; set; } = false;

        // 🔹 الكود المرجعي للحضوري
        public string? ReferenceCode { get; set; }

        // 🔹 القوائم المساعدة
        [ValidateNever]
        public List<SelectListItem> Courses { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> Batches { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> QuestionPools { get; set; } = new();
    }
}
