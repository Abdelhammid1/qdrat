using System.Collections.Generic;

namespace QdratNew.ViewModels.Users
{
    public class AssignRolesViewModel
    {
        public string UserId { get; set; }
        public string Email { get; set; }

        public List<RoleSelectionViewModel> Roles { get; set; } = new();
      


        public bool RequiresConfirmation { get; set; }
        public string? ConfirmPassword { get; set; }

    }

    public class RoleSelectionViewModel
    {
        public string RoleName { get; set; }
        public string DisplayName { get; set; } // عربي
        public bool IsSelected { get; set; }
    }
}
