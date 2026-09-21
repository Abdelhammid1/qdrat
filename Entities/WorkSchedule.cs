using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class WorkSchedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DayOfWeek Day { get; set; } // يوم الأسبوع: الأحد - الإثنين...

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public bool IsBreak { get; set; } = false; // هل هو وقت راحة؟

        // ✅ إذا مرتبط بمدرب معين
        [ForeignKey("Instructor")]
        public int? InstructorId { get; set; }
        public Instructor Instructor { get; set; }

        // ✅ إذا كان وقت عمل عام للمعهد
        public bool IsForCenter { get; set; } = false;

        public string Notes { get; set; }
    }
}
