using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.PartnerSubscriptions
{
    public class PartnerSubscriptionFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        public int PartnerId { get; set; }

        // ===== مدة العقد =====
        [Required]
        [Display(Name = "تاريخ بداية العقد")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "تاريخ نهاية العقد")]
        public DateTime EndDate { get; set; }

        // ===== حدود الاستخدام =====
        [Display(Name = "الحد الأقصى للطلاب النشطين")]
        public int? MaxActiveStudents { get; set; }

        [Display(Name = "السماح بتعدد الفروع")]
        public bool AllowMultipleBranches { get; set; }

        // ===== Core =====
        [Display(Name = "بنك الأسئلة")]
        public bool CanUseQuestionBank { get; set; }

        [Display(Name = "الواجبات")]
        public bool CanCreateHomework { get; set; }

        [Display(Name = "الاختبارات")]
        public bool CanCreateExams { get; set; }

        // ===== Assessment =====
        [Display(Name = "اختبارات تحديد المستوى")]
        public bool CanUsePlacementExams { get; set; }

        [Display(Name = "اختبارات مؤشر الأداء")]
        public bool CanUsePerformanceIndicatorExams { get; set; }

        // ===== Support =====
        [Display(Name = "الخطط العلاجية")]
        public bool CanUseRemedialPlans { get; set; }

        [Display(Name = "الجلسات العلاجية")]
        public bool CanUseRemedialSessions { get; set; }

        [Display(Name = "المهارات التعزيزية")]
        public bool CanUseReinforcementSkills { get; set; }

        // ===== Content & AI =====
        [Display(Name = "المحتوى التعليمي")]
        public bool CanAccessEducationalContent { get; set; }

        [Display(Name = "تحليلات الذكاء الاصطناعي")]
        public bool CanUseAIAnalytics { get; set; }

        // ===== Data Access =====
        [Display(Name = "الوصول للبيانات بعد انتهاء العقد حتى")]
        public DateTime? AccessUntilDate { get; set; }
    }

}
