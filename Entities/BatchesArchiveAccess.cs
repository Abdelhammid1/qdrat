namespace QdratNew.Entities
{
    public class BatchesArchiveAccess
    {
        public int Id { get; set; }

        public int BatchId { get; set; }
        public Batch Batch { get; set; } = null!;

        /// <summary>المستخدم الذي يملك صلاحية الوصول لهذه الدفعة المؤرشفة</summary>
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        /// <summary>المالك أو المبرمج الذي منح الصلاحية</summary>
        public string GrantedByUserId { get; set; } = string.Empty;
        public ApplicationUser GrantedByUser { get; set; } = null!;

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
