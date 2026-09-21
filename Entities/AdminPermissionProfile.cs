using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class AdminPermissionProfile
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; }  // مثال: مشرف محتوى

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsSystemProfile { get; set; } = false; // Owner / SuperAdmin

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<AdminPermission> Permissions { get; set; }
            = new List<AdminPermission>();
    }
}
