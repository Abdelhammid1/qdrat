namespace QdratNew.ViewModels.Students
{
    public class StudentActivity
    {
        public StudentActivity(string title, string timeAgo, string icon)
        {
            Title = title;
            TimeAgo = timeAgo;
            Icon = icon;
        }

        public string Title { get; set; }
        public string TimeAgo { get; set; }
        public string Icon { get; set; }
    }

}
