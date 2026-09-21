namespace QdratNew.ViewModels.Admin.EmployeeDashboard
{
    public class EmployeeKpiCardVm
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string IconCssClass { get; set; } = string.Empty;
        public string AccentCssClass { get; set; } = string.Empty;
        public string? TargetUrl { get; set; }
    }
}
