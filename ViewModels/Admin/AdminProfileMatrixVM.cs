using QdratNew.Enums;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Admin
{
    public class AdminProfileMatrixVM
    {
        public int ProfileId { get; set; }
        public string ProfileName { get; set; }
        public string? Description { get; set; }

        public List<AdminControllerPermissionVM> Controllers { get; set; } = new();
    }

    public class AdminControllerPermissionVM
    {
        public string ControllerName { get; set; }
        public string DisplayName { get; set; }

        public AdminControllerAccessLevel AccessLevel { get; set; }
        public List<AdminActionVM> CustomActions { get; set; } = new();

        public bool HasCustom { get; set; }
    }
}
