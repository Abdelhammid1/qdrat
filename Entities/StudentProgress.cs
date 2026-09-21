using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class StudentProgress
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Student")]
        public int StudentID { get; set; }
        public required QdratNew.Entities.Student Student { get; set; }  // ✅ استخدام المسار الكامل لتجنب الالتباس

        [ForeignKey("Course")]
        public int CourseID { get; set; }
        public required Course Course { get; set; }

        public DateTime Date { get; set; }
        public int CompletedLessons { get; set; }
        public int CompletedExercises { get; set; }
        public double ProgressPercentage { get; set; }

        public required string Topic { get; set; }
        public double Score { get; set; }
        public required string DifficultyLevel { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
