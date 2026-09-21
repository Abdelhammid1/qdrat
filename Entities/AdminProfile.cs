using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class AdminProfile
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// بروفايل نظام (Owner / SuperAdmin) لا يُدار من UI
        /// </summary>
        public bool IsSystemProfile { get; set; } = false;

        public ICollection<AdminProfileControllerPermission> ControllerPermissions { get; set; }
            = new List<AdminProfileControllerPermission>();
    }
}
