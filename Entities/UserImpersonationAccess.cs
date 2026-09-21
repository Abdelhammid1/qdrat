namespace QdratNew.Entities
{
    public class UserImpersonationAccess
    {
        public int Id { get; set; }

        /// <summary>المستخدم الذي يملك صلاحية "الدخول كمستخدم"</summary>
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        /// <summary>المالك أو المبرمج الذي منح الصلاحية</summary>
        public string GrantedByUserId { get; set; } = string.Empty;
        public ApplicationUser GrantedByUser { get; set; } = null!;

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
