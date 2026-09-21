using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Exam
{
    public class PlacementExamAdvancedCreateVm
    {
        // ✅ العنوان (افتراضي ثابت ولكن يمكن تعديله)
        [Required(ErrorMessage = "العنوان مطلوب")]
        [Display(Name = "عنوان الاختبار")]
        public string Title { get; set; } = "اختبار تحديد المستوى";

        // ✅ الفترة الزمنية
        [Required(ErrorMessage = "تاريخ البداية مطلوب")]
        [Display(Name = "تاريخ ووقت البداية")]
        public DateTime StartDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "تاريخ النهاية مطلوب")]
        [Display(Name = "تاريخ ووقت النهاية")]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(1);

        // ✅ عدد الأسئلة
        [Required(ErrorMessage = "عدد الأسئلة مطلوب")]
        [Range(5, 200, ErrorMessage = "عدد الأسئلة يجب أن يكون بين 5 و 200")]
        [Display(Name = "عدد الأسئلة")]
        public int TotalQuestions { get; set; } = 45;

        // ✅ نسبة النجاح
        [Required(ErrorMessage = "نسبة النجاح مطلوبة")]
        [Range(1, 100, ErrorMessage = "نسبة النجاح يجب أن تكون بين 1 و 100")]
        [Display(Name = "نسبة النجاح (%)")]
        public int PassingPercentage { get; set; } = 60;

        // ✅ مدة الاختبار (بالدقائق)
        [Required(ErrorMessage = "مدة الاختبار مطلوبة")]
        [Range(5, 300, ErrorMessage = "مدة الاختبار يجب أن تكون بين 5 و 300 دقيقة")]
        [Display(Name = "مدة الاختبار (دقائق)")]
        public int DurationMinutes { get; set; } = 45;

        // ✅ طريقة توزيع الأسئلة
        [Display(Name = "طريقة توزيع الأسئلة")]
        public bool IsManualSelection { get; set; } = false; // false = تلقائي، true = يدوي

        // ✅ ترتيب الأسئلة
        [Display(Name = "ترتيب الأسئلة")]
        public bool IsRandomOrder { get; set; } = true;

        // ✅ عرض اسم المحور
        [Display(Name = "عرض اسم المحور على كل سؤال")]
        public bool ShowSectionName { get; set; } = true;

        // ✅ ربط الاختبار بالدفعة أو الطالب
        [Display(Name = "الدفعة")]
        public int? BatchId { get; set; }

        [Display(Name = "الطالب")]
        public int? StudentId { get; set; }

        [Display(Name = "الدورة")]
        public int? CourseId { get; set; }

        // ✅ عناصر القوائم المنسدلة
        [ValidateNever]
        public List<SelectListItem> Batches { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> Students { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> Courses { get; set; } = new();

        // ✅ خيار إعادة استخدام اختبار سابق
        [Display(Name = "استخدام اختبار سابق")]
        public int? ExistingExamId { get; set; }

        [ValidateNever]
        public List<SelectListItem> ExistingExams { get; set; } = new();





        public double PassPercentage { get; set; } = 70;
        public bool IsRandomized { get; set; } = true;
        public bool ShowSectionTitle { get; set; } = false;

      




    }
}
