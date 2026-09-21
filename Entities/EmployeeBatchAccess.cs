using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    /// <summary>
    /// صلاحية وصول الموظف (Employee role) لدفعة بعينها وميزة محددة
    /// تُمنح حصراً من المالك أو المبرمج
    /// </summary>
    public class EmployeeBatchAccess
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

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
    }
}
