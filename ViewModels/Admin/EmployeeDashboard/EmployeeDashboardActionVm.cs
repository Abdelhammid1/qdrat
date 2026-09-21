namespace QdratNew.ViewModels.Admin.EmployeeDashboard
{
    public class EmployeeDashboardActionVm
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Area { get; set; } = "Admin";
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public object? RouteValues { get; set; }
        public string IconCssClass { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
    }
}
