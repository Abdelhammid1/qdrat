using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class InstructorWorkSchedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Instructor")]
        public int InstructorId { get; set; }
        public Instructor Instructor { get; set; }

        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public TimeSpan? BreakStart { get; set; }

        public TimeSpan? BreakEnd { get; set; }

        public bool IsWorkingDay { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
