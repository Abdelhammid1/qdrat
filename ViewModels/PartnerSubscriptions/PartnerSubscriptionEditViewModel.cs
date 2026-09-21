using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.PartnerSubscriptions
{
    public class PartnerSubscriptionEditViewModel
    {
        [Required]
        public int Id { get; set; }

        public int PartnerId { get; set; }

        public string PartnerName { get; set; } = string.Empty;

        // =========================
        // 🔹 مدة التعاقد
        // =========================
        [Required]
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
        // 🔹 صلاحيات
        // =========================
        public bool AllowMultipleBranches { get; set; }

        public bool CanUseQuestionBank { get; set; }

        public bool CanCreateHomework { get; set; }

        public bool CanCreateExams { get; set; }
        public bool CanUseProfessionalModels { get; set; }

        public bool CanUsePlacementExams { get; set; }

        public bool CanUsePerformanceIndicatorExams { get; set; }

        public bool CanUseReinforcementSkills { get; set; }

        public bool CanUseRemedialPlans { get; set; }

        public bool CanUseRemedialSessions { get; set; }

        public bool CanAccessEducationalContent { get; set; }

        // =========================
        // 🔹 وصول بعد انتهاء العقد
        // =========================
        public DateTime? AccessUntilDate { get; set; }
        public List<PartnerSubscriptionCourseVM> Courses { get; set; }
    = new();
        public int? MaxInstructors { get; set; }
    }
}
