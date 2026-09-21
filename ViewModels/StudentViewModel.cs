using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Entities;
using QdratNew.ViewModels.Students; // ← تأكد من وجود هذا النيمسبيس للـ StudentPredictionViewModel

namespace QdratNew.ViewModels
{
    public class StudentViewModel
    {
        // ✅ بيانات أساسية
        public int StudentID { get; set; }

        [Required(ErrorMessage = "الرقم القومي مطلوب")]
        [StringLength(14, MinimumLength = 10, ErrorMessage = "يجب أن يكون الرقم القومي بين 10 و 14 رقم")]
        public string NationalID { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "الجنس مطلوب")]
        public string Gender { get; set; }

        public int? Age { get; set; }

        [Required(ErrorMessage = "اسم المدرسة مطلوب")]
        public string School { get; set; }

        [Required(ErrorMessage = "المرحلة التعليمية مطلوبة")]
        public string Level { get; set; }

        // ✅ ربط بالفرع والدفعة وولي الأمر
        [Required(ErrorMessage = "يجب اختيار الفرع")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الدفعة")]
        public int BatchId { get; set; }

        public int? ParentId { get; set; }

        [Display(Name = "الحالة")]
        public string EnrollmentStatus { get; set; } = "نشط";

        // ✅ بيانات التواصل
        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        [Phone]
        public string? WhatsAppNumber { get; set; }

        [Display(Name = "حساب المستخدم")]
        public string? UserId { get; set; }

        // ✅ قوائم مساعدة للـ Dropdowns
        public List<SelectListItem> Branches { get; set; } = new();
        public List<SelectListItem> Batches { get; set; } = new();
        public List<SelectListItem> Parents { get; set; } = new();
        public List<SelectListItem> Users { get; set; } = new();

        // ✅ الخطط العلاجية (العلاقة المرجعية)
        public ICollection<RemedialPlan> RemedialPlans { get; set; } = new List<RemedialPlan>();

        // ✅ نتيجة التنبؤ بالذكاء الاصطناعي
        public StudentPredictionViewModel? LastPrediction { get; set; }

        // ✅ للانضمام لعدة دفعات
        public List<int> BatchIds { get; set; } = new List<int>();
        public List<string> BatchNames { get; set; } = new List<string>();




    }
}
