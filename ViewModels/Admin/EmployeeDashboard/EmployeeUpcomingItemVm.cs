namespace QdratNew.ViewModels.Admin.EmployeeDashboard
{
    public class EmployeeUpcomingItemVm
    {
        public string TypeLabel { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public DateTime ScheduledAt { get; set; }
        public string? DetailsUrl { get; set; }
    }
}
