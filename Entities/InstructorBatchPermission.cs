using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    /// <summary>
    /// صلاحية وصول المدرب لدفعة بعينها وميزة محددة (حضور / واجبات / اختبارات / عرض الدفعة)
    /// تُمنح حصراً من المالك أو المبرمج
    /// </summary>
    public class InstructorBatchPermission
    {
        public int Id { get; set; }

        [Required]
        public int InstructorId { get; set; }
        public Instructor Instructor { get; set; } = null!;

        [Required]
        public int BatchId { get; set; }
        public Batch Batch { get; set; } = null!;

        [Required]
        public InstructorBatchFeature Feature { get; set; }

        public bool IsGranted { get; set; } = true;

        [Required]
        public string GrantedByUserId { get; set; } = null!;
        public ApplicationUser GrantedByUser { get; set; } = null!;

        public DateTime GrantedAt { get; set; } = DateTime.Now;

        public string? Notes { get; set; }
    }

    public enum InstructorBatchFeature
    {
        ViewBatch = 1,
        Attendance = 2,
        Homework = 3,
        Exams = 4
    }
}
