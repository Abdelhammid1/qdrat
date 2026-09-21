namespace QdratNew.ViewModels.Admin.EmployeeDashboard
{
    public class EmployeeDashboardModuleVm
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconCssClass { get; set; } = string.Empty;
        public string AccentCssClass { get; set; } = string.Empty;
        public List<EmployeeDashboardActionVm> Actions { get; set; } = new();
    }
}
