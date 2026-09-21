namespace QdratNew.ViewModels.Admin.EmployeeDashboard
{
    public class EmployeeDashboardViewModel
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string? ProfileName { get; set; }
        public DateTime Today { get; set; } = DateTime.Now;

        public int AllowedModulesCount { get; set; }
        public int ReportActionsCount { get; set; }
        public int NotificationActionsCount { get; set; }

        public List<EmployeeDashboardModuleVm> Modules { get; set; } = new();
        public List<EmployeeKpiCardVm> KpiCards { get; set; } = new();
        public List<EmployeeActionQueueItemVm> ActionQueue { get; set; } = new();
        public List<EmployeeUpcomingItemVm> UpcomingItems { get; set; } = new();
    }
}
