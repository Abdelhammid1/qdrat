using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class AdminProfileControllerPermission
    {
        public int Id { get; set; }

        public int AdminProfileId { get; set; }
        public AdminProfile AdminProfile { get; set; }

        /// <summary>
        /// اسم الكنترولر الحقيقي (Questions, ExamAssignments, ...)
        /// </summary>
        [Required, MaxLength(150)]
        public string ControllerName { get; set; }

        public AdminControllerAccessLevel AccessLevel { get; set; }
            = AdminControllerAccessLevel.None;

        /// <summary>
        /// هل لهذا الكنترولر صلاحيات متخصصة؟
        /// (زر Custom في UI)
        /// </summary>
        public bool HasCustomPermissions { get; set; } = false;

        public ICollection<AdminProfileCustomPermission> CustomPermissions { get; set; }
            = new List<AdminProfileCustomPermission>();
    }
}
