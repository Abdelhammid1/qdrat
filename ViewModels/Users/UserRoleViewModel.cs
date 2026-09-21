using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels
{
    public class UserRoleViewModel
    {
        public string UserId { get; set; }
        public string SelectedRole { get; set; }

        public List<SelectListItem> AvailableRoles { get; set; }
    }
}
