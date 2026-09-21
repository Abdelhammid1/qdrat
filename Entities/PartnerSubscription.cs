using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class PartnerSubscription
    {
        [Key]
        public int Id { get; set; }

        // =========================
        // 🔹 الربط
        // =========================
        [Required]
        public int PartnerId { get; set; }

        [ForeignKey(nameof(PartnerId))]
        public Partner Partner { get; set; }

        // =========================
        // 🔹 مدة الاشتراك
        // =========================
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        // =========================
        // 🔹 حدود الاستخدام
        // =========================
        /// <summary>
        /// الحد الأقصى للطلاب النشطين
        /// null = غير محدود
        /// </summary>
        public int? MaxActiveStudents { get; set; }

        public bool AllowMultipleBranches { get; set; } = false;

        // =========================
        // 🔹 Core Features
        // =========================
        public bool CanUseQuestionBank { get; set; } = true;
        public bool CanCreateHomework { get; set; } = true;
        public bool CanCreateExams { get; set; } = true;
        public bool CanUseProfessionalModels { get; set; }


        // =========================
        // 🔹 Assessment Layer
        // =========================
        public bool CanUsePlacementExams { get; set; } = false;
        public bool CanUsePerformanceIndicatorExams { get; set; } = false;

        // =========================
        // 🔹 Academic Support
        // =========================
        public bool CanUseRemedialPlans { get; set; } = false;
        public bool CanUseRemedialSessions { get; set; } = false;
        public bool CanUseReinforcementSkills { get; set; } = false;

        // =========================
        // 🔹 Content & AI
        // =========================
        public bool CanAccessEducationalContent { get; set; } = false;
        public bool CanUseAIAnalytics { get; set; } = false;
        public int? MaxInstructors { get; set; } // مؤقتًا Nullable
        // =========================
        // 🔹 Data Access After Expiry
        // =========================
        public DateTime? AccessUntilDate { get; set; }

        // =========================
        // 🔹 System Fields
        // =========================
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public bool IsActive
        {
            get
            {
                var today = DateTime.Today;
                return today >= StartDate.Date && today <= EndDate.Date;
            }
        }
        public ICollection<PartnerSubscriptionCourse> SubscriptionCourses { get; set; }
    = new List<PartnerSubscriptionCourse>();

    }
}
