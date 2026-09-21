using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class AdminUserProfile
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public int? AdminProfileId { get; set; }
        public AdminProfile AdminProfile { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
