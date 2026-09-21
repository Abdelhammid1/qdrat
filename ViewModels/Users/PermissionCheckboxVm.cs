namespace QdratNew.ViewModels.Users
{
    public class PermissionCheckboxVm
    {
        public PermissionCheckboxVm() { }

        public PermissionCheckboxVm(string value, string display, bool granted)
        {
            Value = value;
            Display = display;
            IsGranted = granted;
        }

        public string Value { get; set; }
        public string Display { get; set; }
        public bool IsGranted { get; set; }

    }
}
