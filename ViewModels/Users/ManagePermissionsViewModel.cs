namespace QdratNew.ViewModels.Users
{
    public class ManagePermissionsViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }

        public List<PermissionCheckboxVm> Permissions { get; set; } = new();
    }
}
