using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.PartnerSubscriptions
{
    public class PartnerSubscriptionCreateViewModel
    {
        // =========================
        // 🔹 تعريف المدرسة
        // =========================
        [Required]
        public int PartnerId { get; set; }

        public string PartnerName { get; set; } = string.Empty;

        // =========================
        // 🔹 مدة التعاقد
        // =========================
        [Required]
        [Display(Name = "تاريخ بداية العقد")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "تاريخ نهاية العقد")]
        public DateTime EndDate { get; set; }

        // =========================
        // 🔹 حدود الاستخدام
        // =========================
        [Display(Name = "الحد الأقصى للطلاب النشطين")]
        public int? MaxActiveStudents { get; set; }

        // =========================
        // 🔹 صلاحيات هيكلية
        // =========================
        [Display(Name = "السماح بعدة فروع")]
        public bool AllowMultipleBranches { get; set; }

        [Display(Name = "استخدام بنك الأسئلة")]
        public bool CanUseQuestionBank { get; set; }

        // =========================
        // 🔹 واجبات واختبارات
        // =========================
        [Display(Name = "إضافة واجبات")]
        public bool CanCreateHomework { get; set; }

        [Display(Name = "إضافة اختبارات")]
        public bool CanCreateExams { get; set; }

        // =========================
        // 🔹 اختبارات متقدمة
        // =========================
        [Display(Name = "اختبارات تحديد المستوى")]
        public bool CanUsePlacementExams { get; set; }

        [Display(Name = "اختبارات مؤشر الأداء")]
        public bool CanUsePerformanceIndicatorExams { get; set; }

        // =========================
        // 🔹 مهارات وخطط علاجية
        // =========================
        [Display(Name = "المهارات التعزيزية")]
        public bool CanUseReinforcementSkills { get; set; }

        [Display(Name = "الخطة العلاجية")]
        public bool CanUseRemedialPlans { get; set; }

        [Display(Name = "الجلسات العلاجية")]
        public bool CanUseRemedialSessions { get; set; }

        // =========================
        // 🔹 المحتوى التعليمي
        // =========================
        [Display(Name = "المحتوى التعليمي")]
        public bool CanAccessEducationalContent { get; set; }

        // =========================
        // 🔹 الوصول بعد انتهاء العقد
        // =========================
        [Display(Name = "السماح بالوصول للبيانات حتى")]
        public DateTime? AccessUntilDate { get; set; }

        public List<PartnerSubscriptionCourseVM> Courses { get; set; }
    = new();
        public bool CanUseProfessionalModels { get; set; }
        public int? MaxInstructors { get; set; }
    }
}
