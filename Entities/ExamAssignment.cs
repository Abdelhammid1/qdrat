using System;
using System.ComponentModel.DataAnnotations;
using QdratNew.Entities;

namespace QdratNew.Entities
{
    public class ExamAssignment
    {
        public int Id { get; set; }

        [Required]
        public int ExamId { get; set; }
        public Exam Exam { get; set; }

        [Required]
        public int StudentId { get; set; }
        public Student Student { get; set; }

        [Display(Name = "تاريخ الإرسال")]
        public DateTime AssignedAt { get; set; } = DateTime.Now;

        [Display(Name = "تاريخ الاستحقاق")]
        public DateTime? DueDate { get; set; }
    }
}
