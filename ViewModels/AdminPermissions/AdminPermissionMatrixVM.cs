using QdratNew.Enums;
using System.Collections.Generic;

namespace QdratNew.ViewModels.AdminPermissions
{
    public class AdminPermissionMatrixVM
    {
        public int ProfileId { get; set; }
        public string ProfileName { get; set; }
        public bool IsSystemProfile { get; set; }

        public List<AdminPermissionModuleVM> Modules { get; set; }
            = new List<AdminPermissionModuleVM>();
    }

    public class AdminPermissionModuleVM
    {
        public int ModuleId { get; set; }
        public string ModuleKey { get; set; }
        public string ModuleName { get; set; }

        public AdminPermissionLevel SelectedLevel { get; set; }
    }
}
