using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class AdminProfileCustomPermission
    {
        public int Id { get; set; }

        public int AdminProfileControllerPermissionId { get; set; }
        public AdminProfileControllerPermission ControllerPermission { get; set; }

        /// <summary>
        /// اسم الأكشن (Create, Edit, Delete, Import, ReSend...)
        /// </summary>
        [Required, MaxLength(150)]
        public string ActionName { get; set; }

        public bool IsAllowed { get; set; } = false;
    }
}
