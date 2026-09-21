using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class InstituteWorkingHours
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DayOfWeek DayOfWeek { get; set; }  // السبت - الأحد - إلخ

        [Required]
        public TimeSpan StartTime { get; set; }   // بداية الدوام

        [Required]
        public TimeSpan EndTime { get; set; }     // نهاية الدوام

        public TimeSpan? BreakStart { get; set; } // بداية الاستراحة (اختياري)
        public TimeSpan? BreakEnd { get; set; }   // نهاية الاستراحة (اختياري)

        // ربط بفرع معين (اختياري)
        [ForeignKey("Branch")]
        public int? BranchId { get; set; }
        public Branch Branch { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsWorkingDay { get; set; } = true;

    }
}
