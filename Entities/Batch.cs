using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class Batch
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الدفعة مطلوب")]
        [Display(Name = "اسم الدفعة")]
        public string Name { get; set; }

        [Required(ErrorMessage = "تاريخ البدء مطلوب")]
        [Display(Name = "تاريخ بدء الدفعة")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Display(Name = "تاريخ نهاية الدفعة")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Required(ErrorMessage = "يرجى اختيار الدورة التدريبية المرتبطة")]
        [Display(Name = "الدورة التدريبية")]
        public int CourseId { get; set; }

        public BatchGender Gender { get; set; }

        public ICollection<BatchLessonCompletion> CompletedLessons { get; set; } = new List<BatchLessonCompletion>();

        public Course Course { get; set; }

        public bool IsDeleted { get; set; } = false;

        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedByUserId { get; set; }

        public int BranchId { get; set; }
        public Branch Branch { get; set; }

        public bool IsActive { get; set; } = true;

        [InverseProperty(nameof(StudentBatchEnrollment.Batch))]
        public ICollection<StudentBatchEnrollment> StudentBatchEnrollments { get; set; }
        = new List<StudentBatchEnrollment>();


    }

    public enum BatchGender
    {
        ذكور = 0,
        إناث = 1,
        مختلط = 2
    }
}
