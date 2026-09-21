namespace QdratNew.ViewModels.Admin.EmployeeDashboard
{
    public class EmployeeActionQueueItemVm
    {
        public string ModuleKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public DateTime? DueAt { get; set; }
        public string PrimaryActionTitle { get; set; } = string.Empty;
        public string PrimaryActionUrl { get; set; } = string.Empty;
        public bool IsInlineAction { get; set; }
        public string? InlineActionEndpoint { get; set; }
    }
}
