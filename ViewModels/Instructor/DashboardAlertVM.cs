namespace QdratNew.ViewModels.Instructor
{

    public class DashboardAlertVM
    {
        public string Type { get; set; } = "info";   // info | warning | danger | neutral
        public string Message { get; set; } = "";
        public string Action { get; set; } = "";
    }
}
