using QdratNew.Enums;

namespace QdratNew.Helpers
{
    public static class AdminPermissionRule
    {
        public static bool CanRead(AdminPermissionLevel level)
        {
            return level == AdminPermissionLevel.Read
                || level == AdminPermissionLevel.ReadWrite
                || level == AdminPermissionLevel.Delete;
        }

        public static bool CanWrite(AdminPermissionLevel level)
        {
            return level == AdminPermissionLevel.ReadWrite
                || level == AdminPermissionLevel.Delete;
        }

        public static bool CanDelete(AdminPermissionLevel level)
        {
            return level == AdminPermissionLevel.Delete;
        }
    }
}
