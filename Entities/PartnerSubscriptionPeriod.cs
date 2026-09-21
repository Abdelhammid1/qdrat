using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class PartnerSubscriptionPeriod
    {
        [Key]
        public int Id { get; set; }

        // =========================
        // 🔹 العقد الأساسي
        // =========================
        [Required]
        public int PartnerSubscriptionId { get; set; }

        [ForeignKey(nameof(PartnerSubscriptionId))]
        public PartnerSubscription PartnerSubscription { get; set; }

        // =========================
        // 🔹 مدة الفترة
        // =========================
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        // =========================
        // 🔹 عدد الطلاب في هذه الفترة
        // =========================
        /// <summary>
        /// عدد الطلاب المسموح لهم باستلام خدمات جديدة
        /// </summary>
        public int? MaxStudents { get; set; }

        // =========================
        // 🔹 بيانات تنظيمية
        // =========================
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // =========================
        // 🔹 حالة الفترة (غير مخزنة)
        // =========================
        [NotMapped]
        public bool IsActive =>
            DateTime.Today >= StartDate &&
            DateTime.Today <= EndDate;

        // =========================
        // 🔹 الطلاب المرتبطون بهذه الفترة
        // =========================
        public ICollection<Student> Students { get; set; } = new List<Student>();
    }
}
