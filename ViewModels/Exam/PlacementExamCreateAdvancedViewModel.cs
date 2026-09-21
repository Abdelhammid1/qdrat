using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    public class PlacementExamCreateAdvancedViewModel
    {
        [Required(ErrorMessage = "اختيار الدورة مطلوب")]
        [Display(Name = "الدورة")]
        public int CourseId { get; set; }

        [Display(Name = "الدفعة (اختياري)")]
        public int? BatchId { get; set; }

        [Display(Name = "الطالب (اختياري)")]
        public int? StudentId { get; set; }

        [Required(ErrorMessage = "عدد الأسئلة مطلوب")]
        [Display(Name = "عدد الأسئلة")]
        [Range(1, 500, ErrorMessage = "عدد الأسئلة يجب أن يكون بين 1 و 500")]
        public int TotalQuestions { get; set; } = 60;

        [Required(ErrorMessage = "مدة الاختبار مطلوبة")]
        [Display(Name = "مدة الاختبار (بالدقائق)")]
        [Range(5, 300, ErrorMessage = "المدة يجب أن تكون بين 5 و 300 دقيقة")]
        public int DurationMinutes { get; set; } = 60;

        [Display(Name = "نسبة النجاح (%)")]
        [Range(1, 100, ErrorMessage = "نسبة النجاح يجب أن تكون بين 1 و 100")]
        public int PassingScore { get; set; } = 60;

        [Display(Name = "عرض الأسئلة بترتيب عشوائي؟")]
        public bool IsRandomized { get; set; } = true;

        [Display(Name = "الاختبار حضوري ويتطلب كود مرجعي")]
        public bool IsInLab { get; set; } = false;

        [Display(Name = "عرض اسم المحور على كل سؤال؟")]
        public bool ShowSectionName { get; set; } = true;

        [Display(Name = "تاريخ ووقت بدء الاختبار")]
        [DataType(DataType.DateTime)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "تاريخ ووقت انتهاء الاختبار")]
        [DataType(DataType.DateTime)]
        public DateTime? EndDate { get; set; }


        [ValidateNever]
        public List<PlacementExamSectionSelectionVm> SectionSelections { get; set; } = new();
        // ✅ القوائم المنسدلة
        [ValidateNever]
        public List<SelectListItem> Courses { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> Batches { get; set; } = new();
        [ValidateNever]
        public List<SelectListItem> Students { get; set; } = new();
    }
}
