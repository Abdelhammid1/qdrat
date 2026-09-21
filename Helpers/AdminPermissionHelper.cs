using QdratNew.Enums;

namespace QdratNew.Helpers
{
    public static class AdminPermissionHelper
    {
        public static bool CanRead(AdminPermissionLevel level)
            => level >= AdminPermissionLevel.Read;

        public static bool CanWrite(AdminPermissionLevel level)
            => level >= AdminPermissionLevel.ReadWrite;

        public static bool CanDelete(AdminPermissionLevel level)
            => level == AdminPermissionLevel.Delete;
    }
}
