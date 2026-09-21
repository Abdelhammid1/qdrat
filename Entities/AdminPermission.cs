using QdratNew.Enums;

namespace QdratNew.Entities
{
    public class AdminPermission
    {
        public int Id { get; set; }

        public int ProfileId { get; set; }
        public AdminPermissionProfile Profile { get; set; }

        public int ModuleId { get; set; }
        public AdminPermissionModule Module { get; set; }

        /// <summary>
        /// مستوى الصلاحية:
        /// None = لا صلاحية
        /// Read = عرض
        /// ReadWrite = عرض + تعديل
        /// Delete = عرض + تعديل + حذف (أعلى مستوى)
        /// </summary>
        public AdminPermissionLevel AccessLevel { get; set; }
            = AdminPermissionLevel.None;
    }
}
